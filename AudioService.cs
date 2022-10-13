// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using DiscordAudioTests.Http;
using DiscordAudioTests.Voice;
using Microsoft.Extensions.Logging;
using NextAudio;
using NextAudio.Matroska;
using NextAudio.Matroska.Models;
using YoutubeExplode;
using YoutubeExplode.Videos;

namespace DiscordAudioTests;

public class AudioService
{
    private readonly YoutubeClient _youtubeClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly VoiceGatewayClientManager _voiceClientManager;
    private readonly AudioPlayerManager _playerManager;
    private readonly MatroskaDemuxerOptions _demuxerOptions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AudioService> _logger;

    public AudioService(
        YoutubeClient youtubeClient,
        IHttpClientFactory httpClientFactory,
        VoiceGatewayClientManager voiceClientManager,
        AudioPlayerManager playerManager,
        ILoggerFactory loggerFactory)
    {
        _youtubeClient = youtubeClient;
        _httpClientFactory = httpClientFactory;
        _voiceClientManager = voiceClientManager;
        _playerManager = playerManager;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AudioService>();
        _demuxerOptions = new MatroskaDemuxerOptions()
        {
            DisposeSourceStream = true,
            TrackSelector = (tracks) =>
            {
                foreach (var track in tracks)
                {
                    if (track.Type == MatroskaTrackType.Audio && track.CodecID == "A_OPUS")
                    {
                        return track.TrackNumber;
                    }
                }

                return tracks.First().TrackNumber;
            }
        };
    }

    public bool HasPlayerForContext(ShardedCommandContext context)
    {
        return _playerManager.TryGetPlayerForGuild(context.Guild, out var _);
    }

    public AudioPlayer GetOrCreatePlayerForContext(ShardedCommandContext context)
    {
        if (_playerManager.TryGetPlayerForGuild(context.Guild, out var player))
        {
            return player;
        }

        player = _playerManager.CreatePlayer((SocketTextChannel)context.Channel);

        player.SongStarted += SongStarted;
        player.SongFinished += SongFinished;
        player.PlayerFinished += PlayerFinished;
        player.PlayerException += PlayerException;

        return player;
    }

    public async Task PlayAsync(ShardedCommandContext context, string url)
    {
        var player = GetOrCreatePlayerForContext(context);

        if (player.Disposed)
        {
            player.SongStarted -= SongStarted;
            player.SongFinished -= SongFinished;
            player.PlayerFinished -= PlayerFinished;
            player.PlayerException -= PlayerException;

            await _playerManager.DestroyPlayerAsync(player);
            await _voiceClientManager.DestroyClientForGuildAsync(context.Guild);

            player = GetOrCreatePlayerForContext(context);
        }

        AudioStream sourceStream;

        if (TryParseYtVideoId(url, out var ytVideoId))
        {
            var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(ytVideoId);

            var streamInfo = manifest.GetAudioOnlyStreams()
                            .Where(a => a.AudioCodec.Equals("opus", StringComparison.Ordinal))
                            .OrderByDescending(a => a.Bitrate)
                            .FirstOrDefault();

            sourceStream = YoutubeAudioStream.CreateStream(_httpClientFactory.CreateClient<YoutubeClient>(), streamInfo);
        }
        else
        {
            sourceStream = AudioStream.CreateFromFile(url, new FileAudioStreamOptions()
            {
                RecommendedSynchronicity = RecommendedSynchronicity.Async,
            });
        }

        var demuxer = new MatroskaDemuxer(sourceStream, _demuxerOptions, _loggerFactory);

        player.Queue.Enqueue(demuxer);

        if (!player.IsStarted)
        {
            var user = (SocketGuildUser)context.User;

            var voiceClient = await _voiceClientManager.GetOrCreateClientAsync(user.VoiceChannel, player);

            if (!voiceClient.Started)
            {
                await voiceClient.StartAsync();
            }

            await player.StartAsync();
        }
    }

    private static bool TryParseYtVideoId(string url, out VideoId videoId)
    {
        var result = VideoId.TryParse(url);

        if (result.HasValue)
        {
            videoId = result.Value;
            return true;
        }

        videoId = default;
        return false;
    }

    public async Task StopAsync(ShardedCommandContext context)
    {
        if (!_playerManager.TryGetPlayerForGuild(context.Guild, out var player))
        {
            return;
        }

        await _playerManager.DestroyPlayerAsync(player);

        player.SongStarted -= SongStarted;
        player.SongFinished -= SongFinished;
        player.PlayerFinished -= PlayerFinished;
        player.PlayerException -= PlayerException;

        if (!_voiceClientManager.TryGetVoiceClientForGuild(context.Guild, out var voiceClient))
        {
            return;
        }

        await _voiceClientManager.DestroyClientAsync(voiceClient);
    }

    public static void SongStarted(AudioPlayer player)
    {
        _ = player.TextChannel.SendMessageAsync($"New song started.");
    }

    public static void SongFinished(AudioPlayer player)
    {
        _ = player.TextChannel.SendMessageAsync($"Current song finished.");
    }

    public void PlayerFinished(AudioPlayer player)
    {
        _ = player.TextChannel.SendMessageAsync($"Current queue finished, disconnected.");
        _ = _playerManager.DestroyPlayerAsync(player);
        _ = _voiceClientManager.DestroyClientForGuildAsync(player.Guild);
    }

    public void PlayerException(AudioPlayer player, Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return;
        }

        var guildId = player.Guild.Id;
        var message = exception.Message;

        _logger.LogError(exception, "Player for guild '{GuildId}' throws exception '{Message}'", guildId, message);
    }
}
