// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests
{
    public class AudioPlayer : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly ILogger<AudioPlayer> _logger;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private OggStreamReader _streamReader;
        private TaskCompletionSource _pauseTsc;
        private bool _isStarted;
        private IAudioClient _audioClient;

        public AudioPlayer(IVoiceChannel voiceChannel, ILogger<AudioPlayer> logger)
        {
            VoiceChannel = voiceChannel;
            _logger = logger;
        }

        public IVoiceChannel VoiceChannel { get; }

        public ConcurrentQueue<Stream> Queue { get; } = new();

        public IGuild Guild => VoiceChannel.Guild;

        public bool IsPaused => _pauseTsc != null;

        public bool IsStarted
        {
            get => Volatile.Read(ref _isStarted);
            private set => Volatile.Write(ref _isStarted, value);
        }

        public bool Disposed { get; private set; }


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
                    _streamReader.SeekTo(seekTime);
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

                    if (_streamReader != null)
                    {
                        _streamReader.Dispose();
                    }

                    _streamReader = new OggStreamReader(stream);
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
                    var bytesRead = _streamReader.ReadNextPacket(memoryOwner.Memory.Span);

                    if (bytesRead <= 0)
                    {
                        if (!Queue.TryDequeue(out var stream))
                        {
                            finished = true;
                            return;
                        }

                        _streamReader.Dispose();

                        _streamReader = new OggStreamReader(stream);

                        return;
                    }

                    if (bytesRead > maxBufferLength)
                    {
                        maxBufferLength = bytesRead;
                    }

                    _logger.LogInformation($"Readed {bytesRead} bytes for guild: {Guild.Name}.");
                    await outStream.WriteAsync(memoryOwner.Memory[..bytesRead], _cts.Token);
                });
            }

            _logger.LogInformation($"The max buffer length was {maxBufferLength} for guild: {Guild.Name}.");

            await outStream.FlushAsync();

            await StopAsync();
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

            _streamReader.Dispose();
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
}
