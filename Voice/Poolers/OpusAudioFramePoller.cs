// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DiscordAudioTests.Voice.Encrypt;
using DiscordAudioTests.Voice.Gateway;
using DiscordAudioTests.Voice.Models;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests.Voice.Poolers;


public class OpusAudioFramePoller : IDisposable, IAsyncDisposable
{
    public const int SamplingRate = 48000;
    public const int FrameMillis = 20;
    public const int FrameSamplesPerChannel = SamplingRate / 1000 * FrameMillis;
    public const int MaxSilenceFrames = 5;
    public const int QueueLength = (1000 + (FrameMillis - 1)) / FrameMillis;

    public static readonly ReadOnlyMemory<byte> SilencesFrames = new byte[] { 0xF8, 0xFF, 0xFE };

    private readonly VoiceGatewayClient _client;
    private readonly ILogger<OpusAudioFramePoller> _logger;
    private readonly TaskCompletionSource _preloadedTsc = new();

    private Channel<AudioFrame> _sendingChannel;
    private CancellationTokenSource _cts = new();
    private int _silenceFramesCount;
    private ushort _seq;
    private uint _timestamp;
    private uint _nonce;

    public OpusAudioFramePoller(VoiceGatewayClient client, ILogger<OpusAudioFramePoller> logger)
    {
        _client = client;
        _logger = logger;
    }

    public bool IsStarted { get; private set; }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsStarted)
        {
            return ValueTask.CompletedTask;
        }

        IsStarted = true;

        _ = Task.Run(() => RunAsync(cancellationToken), cancellationToken);

        return ValueTask.CompletedTask;
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (_cts == null)
        {
            _cts = new();
        }

        var startToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        _ = startToken.Token.Register(() =>
        {
            IsStarted = false;
        });

        _sendingChannel = Channel.CreateBounded<AudioFrame>(new BoundedChannelOptions(QueueLength)
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        return Task.WhenAll(RunReaderAsync(), RunWriterAsync()).WaitAsync(startToken.Token);
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsStarted || _cts.Token.IsCancellationRequested)
        {
            return;
        }

        IsStarted = false;

        _cts.Cancel(false);
        _cts.Dispose();
        _cts = null;

        _ = _sendingChannel.Writer.TryComplete();

        await foreach (var frame in _sendingChannel.Reader.ReadAllAsync(cancellationToken))
        {
            frame.MemoryOwner.Dispose();
        }

        _sendingChannel = null;
    }

    private async Task RunReaderAsync()
    {
        await _preloadedTsc.Task.WaitAsync(_cts.Token);

        await _client.SendSpeakingAsync(SpeakingMask.Microphone);

        long nextTick = Environment.TickCount;

        while (!_cts.Token.IsCancellationRequested)
        {
            long tick = Environment.TickCount;
            var dist = nextTick - tick;

            if (dist > 0)
            {
                await Task.Delay((int)dist, _cts.Token);
                continue;
            }

            if (!_sendingChannel.Reader.TryRead(out var frame))
            {
                while ((nextTick - tick) <= 0)
                {
                    if (_silenceFramesCount++ < MaxSilenceFrames)
                    {
                        await WriteFrameAsync(Memory<byte>.Empty, _cts.Token);
                    }

                    nextTick += FrameMillis;
                }

                _ = await _sendingChannel.Reader.WaitToReadAsync(_cts.Token);

                continue;
            }

            await WriteFrameAsync(frame.MemoryOwner.Memory[..frame.Length], _cts.Token);

            frame.MemoryOwner.Dispose();

            nextTick += FrameMillis;

            if (_silenceFramesCount != 0)
            {
                _silenceFramesCount = 0;
            }
        }
    }

    private async ValueTask WriteFrameAsync(Memory<byte> frame, CancellationToken cancellationToken)
    {
        var packetArray = ArrayPool<byte>.Shared.Rent(_client.EncryptionMode.CalculatePacketSize());

        try
        {
            var length = PreparePacket(frame.Span, packetArray);

            var packet = packetArray.AsMemory(0, length);

            _ = await _client.SendFrameAsync(packet, cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(packetArray);
        }
    }

    private int PreparePacket(ReadOnlySpan<byte> frame, Span<byte> packet)
    {
        Rtp.EncodeHeader(_seq, _timestamp, _client.Ssrc, packet);

        frame.CopyTo(packet[Rtp.HeaderSize..]);

        _seq++;
        _timestamp += FrameSamplesPerChannel;

        Span<byte> nonce = stackalloc byte[Sodium.NonceSize];

        _client.EncryptionMode.GenerateNonce(packet[..Rtp.HeaderSize], _nonce++, nonce);

        Span<byte> encrypted = stackalloc byte[Sodium.CalculateTargetSize(frame)];

        _client.EncryptionMode.Encrypt(frame, encrypted, nonce, _client.SecretKey.Span);

        encrypted.CopyTo(packet[Rtp.HeaderSize..]);

        _client.EncryptionMode.AppendNonce(nonce, packet);

        return _client.EncryptionMode.CalculatePacketSize(encrypted.Length);
    }

    private async Task RunWriterAsync()
    {
        await _client.AudioFrameSenderTask.WaitAsync(_cts.Token);

        while (!_cts.Token.IsCancellationRequested)
        {
            var memoryOwner = MemoryPool<byte>.Shared.Rent(1024);

            var bytesReaded = await _client.AudioFrameSender.ProvideFrameAsync(memoryOwner.Memory, _cts.Token);

            var frame = new AudioFrame(memoryOwner, bytesReaded);

            if (!_sendingChannel.Writer.TryWrite(frame))
            {
                while (await _sendingChannel.Writer.WaitToWriteAsync(_cts.Token))
                {
                    if (_sendingChannel.Writer.TryWrite(frame))
                    {
                        break;
                    }
                }
            }

            if (!_preloadedTsc.Task.IsCompleted && _sendingChannel.Reader.Count >= QueueLength)
            {
                _ = _preloadedTsc.TrySetResult();
            }
        }

    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return StopAsync();
    }

    private readonly struct AudioFrame
    {
        public AudioFrame(IMemoryOwner<byte> memoryOwner, int length)
        {
            MemoryOwner = memoryOwner;
            Length = length;
        }

        public IMemoryOwner<byte> MemoryOwner { get; }

        public int Length { get; }
    }
}
