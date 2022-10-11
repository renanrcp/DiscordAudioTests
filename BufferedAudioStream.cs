// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Discord.Audio;

namespace DiscordAudioTests;

public class BufferedAudioStream : AudioOutStream
{
    public const int SamplingRate = 48000;
    public const int FrameMillis = 20;
    public const int FrameSamplesPerChannel = SamplingRate / 1000 * FrameMillis;

    private const int MaxSilenceFrames = 10;

    private readonly struct Frame
    {
        public Frame(IMemoryOwner<byte> buffer, int bytes)
        {
            Buffer = buffer;
            Bytes = bytes;
        }

        public readonly IMemoryOwner<byte> Buffer;
        public readonly int Bytes;
    }

    private static readonly byte[] _silenceFrame = Array.Empty<byte>();

    private readonly IAudioClient _client;
    private readonly AudioStream _next;
    private readonly CancellationTokenSource _disposeTokenSource, _cancelTokenSource;
    private readonly CancellationToken _cancelToken;
    private readonly ConcurrentQueue<Frame> _queuedFrames;
    private readonly SemaphoreSlim _queueLock;
    private readonly int _ticksPerFrame, _queueLength;
    private readonly TaskCompletionSource _preloadedTsc = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TaskCompletionSource _emptyQueueTsc = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _silenceFrames;

    internal BufferedAudioStream(AudioStream next, IAudioClient client, int bufferMillis, CancellationToken cancelToken)
    {
        _next = next;
        _client = client;
        _ticksPerFrame = FrameMillis;
        _queueLength = (bufferMillis + (_ticksPerFrame - 1)) / _ticksPerFrame; //Round up

        _disposeTokenSource = new CancellationTokenSource();
        _cancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_disposeTokenSource.Token, cancelToken);
        _cancelToken = _cancelTokenSource.Token;
        _queuedFrames = new ConcurrentQueue<Frame>();

        _queueLock = new SemaphoreSlim(_queueLength, _queueLength);
        _silenceFrames = MaxSilenceFrames;

        _ = Task.Run(Run, cancelToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposeTokenSource?.Cancel();
            _disposeTokenSource?.Dispose();
            _cancelTokenSource?.Cancel();
            _cancelTokenSource?.Dispose();
            _queueLock?.Dispose();
            _next.Dispose();
        }
        base.Dispose(disposing);
    }

    private async Task Run()
    {
        try
        {
            await _preloadedTsc.Task.WaitAsync(_cancelToken);

            await _client.SetSpeakingAsync(true);

            long nextTick = Environment.TickCount;
            ushort seq = 0;
            uint timestamp = 0;

            while (!_cancelToken.IsCancellationRequested)
            {
                long tick = Environment.TickCount;
                var dist = nextTick - tick;
                if (dist <= 0)
                {
                    if (_queuedFrames.TryDequeue(out var frame))
                    {
                        await TryInvokeEmptyQueue();

                        _next.WriteHeader(seq, timestamp, false);

                        await _next.WriteAsync(frame.Buffer.Memory[..frame.Bytes], _cancelToken);

                        frame.Buffer.Dispose();

                        _ = _queueLock.Release();

                        nextTick += _ticksPerFrame;
                        seq++;
                        timestamp += FrameSamplesPerChannel;

                        if (_silenceFrames != 0)
                        {
                            _silenceFrames = 0;
                        }
                    }
                    else
                    {
                        while ((nextTick - tick) <= 0)
                        {
                            if (_silenceFrames++ < MaxSilenceFrames)
                            {
                                _next.WriteHeader(seq, timestamp, false);
                                await _next.WriteAsync(_silenceFrame, _cancelToken);
                            }

                            nextTick += _ticksPerFrame;
                            seq++;
                            timestamp += FrameSamplesPerChannel;
                        }
                    }
                }
                else
                {
                    await Task.Delay((int)dist/*, _cancelToken*/);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    public override void WriteHeader(ushort seq, uint timestamp, bool missed) { } //Ignore, we use our own timing

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource writeCancelToken = null;
        if (cancellationToken.CanBeCanceled)
        {
            writeCancelToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancelToken);
            cancellationToken = writeCancelToken.Token;
        }
        else
        {
            cancellationToken = _cancelToken;
        }

        _ = await _queueLock.WaitAsync(-1, cancellationToken);

        var memoryOwner = MemoryPool<byte>.Shared.Rent(buffer.Length);

        buffer.CopyTo(memoryOwner.Memory);

        _queuedFrames.Enqueue(new Frame(memoryOwner, buffer.Length));

        if (!_preloadedTsc.Task.IsCompleted && _queuedFrames.Count == _queueLength)
        {
            _ = _preloadedTsc.TrySetResult();
        }

        writeCancelToken?.Dispose();
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_queuedFrames.IsEmpty)
            {
                return;
            }

            if (_emptyQueueTsc == null)
            {
                _emptyQueueTsc = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            await _emptyQueueTsc.Task.WaitAsync(cancellationToken);
            _emptyQueueTsc = null;
        }
    }

    public override Task ClearAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        _queuedFrames.Clear();

        if (_emptyQueueTsc != null)
        {
            _ = _emptyQueueTsc.TrySetResult();
        }

        return Task.CompletedTask;
    }

    private ValueTask TryInvokeEmptyQueue()
    {
        if (_queuedFrames.IsEmpty && _emptyQueueTsc != null)
        {
            _ = _emptyQueueTsc.TrySetResult();
        }

        return ValueTask.CompletedTask;
    }
}
