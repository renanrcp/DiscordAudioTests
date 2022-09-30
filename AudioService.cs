// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Discord;
using YoutubeExplode;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Discord.Commands;
using System;
using Microsoft.Extensions.DependencyInjection;
using NextAudio.Matroska;
using System.IO;
using NextAudio.Matroska.Models;
using System.Net.Http;
using NextAudio;

namespace DiscordAudioTests;

public class AudioService
{
    private readonly ConcurrentDictionary<ulong, AudioPlayer> _players = new();
    private readonly IServiceProvider _provider;
    private readonly YoutubeClient _youtubeClient;
    private readonly HttpClient _httpClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly MatroskaDemuxerOptions _demuxerOptions;
    private readonly ILogger<AudioService> _logger;

    public AudioService(IServiceProvider provider)
    {
        _provider = provider;
        _youtubeClient = provider.GetRequiredService<YoutubeClient>();
        _httpClient = provider.GetRequiredService<HttpClient>();
        _loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        _logger = _loggerFactory.CreateLogger<AudioService>();
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

    public AudioPlayer GetOrCreatePlayerForContext(ShardedCommandContext context)
    {
        if (_players.TryGetValue(context.Guild.Id, out var player) && !player.Disposed)
        {
            return player;
        }

        var voiceChannel = (context.User as IGuildUser).VoiceChannel;

        player = new(voiceChannel, context.Channel, _provider.GetRequiredService<ILogger<AudioPlayer>>());

        _players[context.Guild.Id] = player;

        player.SongStarted += SongStarted;
        player.SongFinished += SongFinished;
        player.PlayerFinished += PlayerFinished;
        player.PlayerException += PlayerException;

        return player;
    }

    public async Task PlayAsync(ShardedCommandContext context, string url)
    {
        var player = GetOrCreatePlayerForContext(context);

        AudioStream sourceStream;

        if (url.StartsWith("http"))
        {
            var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(url);

            var streamInfo = manifest.GetAudioOnlyStreams()
                            .Where(a => a.AudioCodec.Equals("opus", StringComparison.Ordinal))
                            .OrderByDescending(a => a.Bitrate)
                            .FirstOrDefault();

            // var response = await _httpClient.GetAsync(streamInfo.Url, HttpCompletionOption.ResponseHeadersRead);
            // var ytStream = await response.Content.ReadAsStreamAsync();
            // sourceStream = new StreamToAudioStream(ytStream);
            sourceStream = YoutubeAudioStream.CreateStream(_httpClient, streamInfo);
        }
        else
        {
            sourceStream = new StreamToAudioStream(new BufferedStream(File.Open(url, FileMode.Open)));
        }

        var demuxer = new MatroskaDemuxer(sourceStream, _demuxerOptions, _loggerFactory);

        player.Queue.Enqueue(demuxer);
        await player.StartAsync();
    }

    public async Task StopAsync(ShardedCommandContext context)
    {
        var player = GetOrCreatePlayerForContext(context);

        await player.StopAsync();

        player.SongStarted -= SongStarted;
        player.SongFinished -= SongFinished;
        player.PlayerFinished -= PlayerFinished;
        player.PlayerException -= PlayerException;

        _ = _players.TryRemove(context.Guild.Id, out _);
    }

    public static void SongStarted(AudioPlayer player)
    {
        _ = player.TextChannel.SendMessageAsync($"New song started.");
    }

    public static void SongFinished(AudioPlayer player)
    {
        _ = player.TextChannel.SendMessageAsync($"Current song finished.");
    }

    public static void PlayerFinished(AudioPlayer player)
    {
        _ = player.TextChannel.SendMessageAsync($"Current queue finished, disconnected.");
    }

    public void PlayerException(AudioPlayer player, Exception exception)
    {
        _logger.LogCritical(exception, $"Player for guild '{player.VoiceChannel.GuildId}' throws exception '{exception.Message}'");
    }
}
