// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Discord;
using System.IO;
using YoutubeExplode;
using System.Linq;
using Matroska.Muxer;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Discord.Commands;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordAudioTests
{
    public class AudioService
    {
        private readonly ConcurrentDictionary<ulong, AudioPlayer> _players = new();
        private readonly IServiceProvider _provider;
        private readonly YoutubeClient _youtubeClient;

        public AudioService(IServiceProvider provider)
        {
            _provider = provider;
            _youtubeClient = provider.GetRequiredService<YoutubeClient>();
        }

        public AudioPlayer GetOrCreatePlayerForContext(ShardedCommandContext context)
        {
            if (_players.TryGetValue(context.Guild.Id, out var player) && !player.Disposed)
            {
                return player;
            }

            var voiceChannel = (context.User as IGuildUser).VoiceChannel;

            player = new(voiceChannel, _provider.GetRequiredService<ILogger<AudioPlayer>>());

            _players[context.Guild.Id] = player;

            return player;
        }

        public async Task PlayAsync(ShardedCommandContext context, string url)
        {
            var player = GetOrCreatePlayerForContext(context);

            var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(url);

            var streamInfo = manifest.GetAudioOnlyStreams()
                                .Where(a => a.AudioCodec.Equals("opus", StringComparison.Ordinal))
                                .OrderByDescending(a => a.Bitrate)
                                .FirstOrDefault();

            using var sourceStream = await _youtubeClient.Videos.Streams.GetAsync(streamInfo);

            sourceStream.Position = 0;

            using var ms1 = new MemoryStream();

            await sourceStream.CopyToAsync(ms1);

            ms1.Position = 0;

            var ms2 = new MemoryStream();

            MatroskaDemuxer.ExtractOggOpusAudio(ms1, ms2);

            ms2.Position = 0;

            player.Queue.Enqueue(ms2);
            await player.StartAsync();
        }

        public async Task StopAsync(ShardedCommandContext context)
        {
            var player = GetOrCreatePlayerForContext(context);

            await player.StopAsync();

            _ = _players.TryRemove(context.Guild.Id, out _);
        }
    }
}
