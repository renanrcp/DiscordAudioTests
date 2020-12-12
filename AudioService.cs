using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Text;
using DiscordAudioTests.Websockets;
using FFMpegCore;
using System.IO;
using FFMpegCore.Pipes;
using FFMpegCore.Enums;
using System.Buffers;
using NLayer;
using Concentus.Structs;
using Concentus.Oggfile;
using YoutubeExplode;
using System.Linq;

namespace DiscordAudioTests
{
    public class AudioService
    {
        private const string AudioFilePath = "Marshmello & Anne-Marie - FRIENDS (Lyric Video) _OFFICIAL FRIENDZONE ANTHEM.mp3";

        private readonly DiscordShardedClient _client;
        private readonly VoiceGatewayClientFactory _voiceFactory;

        public AudioService(DiscordShardedClient client, VoiceGatewayClientFactory voiceFactory)
        {
            _client = client;
            _voiceFactory = voiceFactory;
        }

        public async Task StartAsync()
        {
            ulong guildId = 463430274823356417;
            ulong channelId = 463471372589465622;

            var guild = _client.GetGuild(guildId);
            var voiceChannel = (IVoiceChannel)guild.GetChannel(channelId);

            // var sessionIdTsc = new TaskCompletionSource<string>();
            // var socketVoiceServerTsc = new TaskCompletionSource<SocketVoiceServer>();

            // _client.UserVoiceStateUpdated += VoiceStateUpdatedAsync;
            // _client.VoiceServerUpdated += VoiceServerUpdatedAsync;

            // Task VoiceStateUpdatedAsync(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
            // {
            //     if (user.Id != _client.CurrentUser.Id || string.IsNullOrWhiteSpace(newState.VoiceSessionId))
            //         return Task.CompletedTask;

            //     sessionIdTsc.TrySetResult(newState.VoiceSessionId);

            //     return Task.CompletedTask;
            // }

            // Task VoiceServerUpdatedAsync(SocketVoiceServer arg)
            // {
            //     if (arg.Guild.Id == guildId)
            //     {
            //         socketVoiceServerTsc.TrySetResult(arg);
            //     }

            //     return Task.CompletedTask;
            // }\
            var youtubeClient = new YoutubeClient();
            var manifest = await youtubeClient.Videos.Streams.GetManifestAsync("https://www.youtube.com/watch?v=CY8E6N5Nzec");
            var streamInfos = manifest.GetAudioOnly();
            var streamInfo = streamInfos
                                .Where(a => a.AudioCodec.Equals("opus"))
                                .FirstOrDefault();

            var sourceStream = await youtubeClient.Videos.Streams.GetAsync(streamInfo);

            var info = await FFProbe.AnalyseAsync(sourceStream);

            var streamReader = new OggStreamReader(sourceStream);
            var reader = new OpusOggReadStream(new OpusDecoder(48000, 2), sourceStream);

            using var audioClient = await voiceChannel.ConnectAsync();
            using var outStream = audioClient.CreateOpusStream();

            sourceStream.Position = 0;

            byte[] buffer = null;

            // _ = Task.Delay(TimeSpan.FromSeconds(30))
            // .ContinueWith((_) =>
            // {
            //     streamReader.SeekTo(TimeSpan.FromSeconds(0));
            // });

            while ((buffer = streamReader.GetNextPacket()) != null)
            {
                await outStream.WriteAsync(buffer.AsMemory());
            }

            await outStream.FlushAsync();

            await voiceChannel.DisconnectAsync();

            // var sessionId = await sessionIdTsc.Task;
            // var voiceServer = await socketVoiceServerTsc.Task;

            // var voiceClient = _voiceFactory.Create(voiceServer, sessionId);

            // await voiceClient.StartAsync();

            // await Task.Delay(TimeSpan.FromMinutes(10));

            // _client.UserVoiceStateUpdated -= VoiceStateUpdatedAsync;
            // _client.VoiceServerUpdated -= VoiceServerUpdatedAsync;
        }
    }
}