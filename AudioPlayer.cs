// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

using NextAudioStream = NextAudio.AudioStream;

namespace DiscordAudioTests;

public delegate void SongStarted(AudioPlayer player);
public delegate void SongFinished(AudioPlayer player);
public delegate void PlayerFinished(AudioPlayer player);
public delegate void PlayerException(AudioPlayer player, Exception exception);

public class AudioPlayer : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger<AudioPlayer> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private NextAudioStream _stream;
    private TaskCompletionSource _pauseTsc;
    private bool _isStarted;
    private IAudioClient _audioClient;

    public AudioPlayer(IVoiceChannel voiceChannel, ISocketMessageChannel textChannel, ILogger<AudioPlayer> logger)
    {
        VoiceChannel = voiceChannel;
        TextChannel = textChannel;
        _logger = logger;
    }

    public IVoiceChannel VoiceChannel { get; }

    public ISocketMessageChannel TextChannel { get; }

    public ConcurrentQueue<NextAudioStream> Queue { get; } = new();

    public IGuild Guild => VoiceChannel.Guild;

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

    public ValueTask StartAsync()
    {
        if (IsStarted)
        {
            return ValueTask.CompletedTask;
        }

        IsStarted = true;

        _ = Task.Run(InternalRunAsync);

        return ValueTask.CompletedTask;
    }

    public ValueTask PauseOrResumeAsync()
    {
        return PerformActionAsync(() =>
        {
            if (!IsPaused)
            {
                _pauseTsc = new();
                return;
            }

            _ = _pauseTsc.TrySetResult();
            _pauseTsc = null;
        });
    }

    public ValueTask StopAsync()
    {
        return DisposeAsync();
    }

    public ValueTask SeekAsync(TimeSpan seekTime)
    {
        return !IsStarted
            ? ValueTask.CompletedTask
            : PerformActionAsync(() =>
            {
                throw new NotImplementedException();
            });
    }

    public ValueTask SkipAsync()
    {
        return !IsStarted
            ? ValueTask.CompletedTask
            : PerformActionAsync(() =>
            {
                if (!Queue.TryDequeue(out var stream))
                {
                    return;
                }

                if (_stream != null)
                {
                    _stream.Dispose();
                }

                _stream = stream;
                SongStarted?.Invoke(this);
            });
    }

    private ValueTask PerformActionAsync(Action action)
    {
        return PerformActionAsync(() =>
        {
            action();
            return ValueTask.CompletedTask;
        });
    }

    private async ValueTask PerformActionAsync(Func<ValueTask> action)
    {
        await _semaphore.WaitAsync();

        try
        {
            await action();
        }
        finally
        {
            _ = _semaphore.Release();
        }
    }


    private async Task InternalRunAsync()
    {
        try
        {
            await SkipAsync();

            _audioClient = await VoiceChannel.ConnectAsync();
            using var outStream = _audioClient.CreateOpusStream();

            using var memoryOwner = MemoryPool<byte>.Shared.Rent(1024);

            var maxBufferLength = 0;
            var finished = false;

            while (!_cts.Token.IsCancellationRequested && !finished)
            {
                if (IsPaused)
                {
                    await _pauseTsc.Task;
                }

                await PerformActionAsync(async () =>
                {
                    var bytesRead = await _stream.ReadAsync(memoryOwner.Memory);

                    if (bytesRead <= 0)
                    {
                        SongFinished.Invoke(this);

                        if (!Queue.TryDequeue(out var stream))
                        {
                            finished = true;
                            PlayerFinished?.Invoke(this);
                            return;
                        }

                        _stream.Dispose();

                        _stream = stream;

                        SongStarted.Invoke(this);

                        return;
                    }

                    if (bytesRead > maxBufferLength)
                    {
                        maxBufferLength = bytesRead;
                    }

                    _logger.LogDebug($"Readed {bytesRead} bytes for guild: {Guild.Name}.");
                    await outStream.WriteAsync(memoryOwner.Memory[..bytesRead], _cts.Token);
                });
            }

            _logger.LogInformation($"The max buffer length was {maxBufferLength} for guild: {Guild.Name}.");

            await outStream.FlushAsync();
        }
        catch (Exception ex)
        {
            PlayerException?.Invoke(this, ex);
        }
        finally
        {
            await StopAsync();
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

        _stream.Dispose();
        _semaphore.Dispose();

        if (IsStarted)
        {
            await VoiceChannel.DisconnectAsync();
        }

        if (_audioClient != null)
        {
            _audioClient.Dispose();
        }


        foreach (var stream in Queue)
        {
            await stream.DisposeAsync();
        }

        Queue.Clear();

        GC.SuppressFinalize(this);
    }
}
