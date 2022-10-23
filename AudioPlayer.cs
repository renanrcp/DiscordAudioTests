// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;
using DiscordAudioTests.Voice.Poolers;
using Microsoft.Extensions.Logging;

using NextAudioStream = NextAudio.AudioStream;

namespace DiscordAudioTests;

public delegate void SongStarted(AudioPlayer player);
public delegate void SongFinished(AudioPlayer player);
public delegate void PlayerFinished(AudioPlayer player);
public delegate void PlayerException(AudioPlayer player, Exception exception);

public class AudioPlayer : IAudioFrameSender, IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger<AudioPlayer> _logger;
    private readonly SemaphoreSlim _semaphore = new(0);

    private NextAudioStream _stream;
    private TaskCompletionSource _pauseTsc;
    private bool _isStarted;

    public AudioPlayer(SocketTextChannel textChannel, ILogger<AudioPlayer> logger)
    {
        TextChannel = textChannel;
        _logger = logger;
    }

    public SocketTextChannel TextChannel { get; }

    public ConcurrentQueue<NextAudioStream> Queue { get; } = new();

    public SocketGuild Guild => TextChannel.Guild;

    public bool IsPaused => _pauseTsc != null;

    public bool IsStarted
    {
        get => Volatile.Read(ref _isStarted);
        private set => Volatile.Write(ref _isStarted, value);
    }

    public bool Disposed { get; private set; }

    public event SongStarted SongStarted;

    public event SongFinished SongFinished;

    public event PlayerFinished PlayerFinished;

    public event PlayerException PlayerException;

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        if (IsStarted)
        {
            return ValueTask.CompletedTask;
        }

        IsStarted = true;

        if (_stream == null && Queue.TryDequeue(out _stream))
        {
            SongStarted?.Invoke(this);
        }

        _ = _semaphore.Release();

        return ValueTask.CompletedTask;
    }

    public async ValueTask<int> ProvideFrameAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            using var frameToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

            if (IsPaused)
            {
                await _pauseTsc.Task.WaitAsync(frameToken.Token);
            }

            var bytesReaded = 0;

            await PerformActionAsync(async () =>
            {
                while (!frameToken.Token.IsCancellationRequested && bytesReaded <= 0)
                {
                    bytesReaded = await _stream.ReadAsync(buffer, frameToken.Token);

                    if (bytesReaded <= 0)
                    {
                        SongFinished?.Invoke(this);

                        await _stream.DisposeAsync();
                        _stream = null;

                        if (!Queue.TryDequeue(out var stream))
                        {
                            return;
                        }

                        _stream = stream;

                        SongStarted?.Invoke(this);
                    }
                }
            }, cancellationToken);

            if (bytesReaded <= 0)
            {
                PlayerFinished?.Invoke(this);
                await StopAsync();
            }

            return bytesReaded;
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException and ObjectDisposedException)
            {
                PlayerException?.Invoke(this, ex);
            }

            if (_cts.IsCancellationRequested)
            {
                return 0;
            }

            if (_stream != null)
            {
                await _stream.DisposeAsync();
                _stream = null;

                SongFinished?.Invoke(this);

                if (Queue.TryDequeue(out _stream))
                {
                    SongStarted.Invoke(this);
                    return 0;
                }
            }

            PlayerFinished?.Invoke(this);

            return 0;
        }
    }

    public ValueTask PauseOrResumeAsync(CancellationToken cancellationToken = default)
    {
        return !IsStarted
            ? ValueTask.CompletedTask
            : PerformActionAsync(() =>
            {
                if (!IsPaused)
                {
                    _pauseTsc = new();
                    return;
                }

                _ = _pauseTsc.TrySetResult();
                _pauseTsc = null;
            }, cancellationToken);
    }

    public ValueTask StopAsync()
    {
        return DisposeAsync();
    }

    public ValueTask SeekAsync(TimeSpan seekTime, CancellationToken cancellationToken = default)
    {
        return !IsStarted
            ? ValueTask.CompletedTask
            : PerformActionAsync(() =>
            {
                throw new NotImplementedException();
            }, cancellationToken);
    }

    public ValueTask SkipAsync(CancellationToken cancellationToken = default)
    {
        return !IsStarted
            ? ValueTask.CompletedTask
            : PerformActionAsync(async () =>
            {
                if (!Queue.TryDequeue(out var stream))
                {
                    return;
                }

                if (_stream != null)
                {
                    await _stream.DisposeAsync();
                    SongFinished?.Invoke(this);
                }

                _stream = stream;
                SongStarted?.Invoke(this);
            }, cancellationToken);
    }

    private ValueTask PerformActionAsync(Action action, CancellationToken cancellationToken)
    {
        return PerformActionAsync(() =>
        {
            action();
            return ValueTask.CompletedTask;
        }, cancellationToken);
    }

    private async ValueTask PerformActionAsync(Func<ValueTask> action, CancellationToken cancellationToken)
    {
        using var semaphoreToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        await _semaphore.WaitAsync(semaphoreToken.Token);

        try
        {
            await action();
        }
        finally
        {
            _ = _semaphore.Release();
        }
    }


    public async ValueTask DisposeAsync()
    {
        if (Disposed)
        {
            return;
        }

        Disposed = true;

        _cts.Cancel(false);
        _cts.Dispose();

        if (_stream != null)
        {
            await _stream.DisposeAsync();
        }

        _semaphore.Dispose();

        foreach (var stream in Queue)
        {
            await stream.DisposeAsync();
        }

        Queue.Clear();

        GC.SuppressFinalize(this);
    }
}
