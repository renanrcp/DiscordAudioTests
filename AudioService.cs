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

namespace DiscordAudioTests
{
    public class AudioService
    {
        private const string AudioFilePath = "Marshmello & Anne-Marie - FRIENDS (Lyric Video) _OFFICIAL FRIENDZONE ANTHEM.mp3";

        private readonly DiscordShardedClient _client;

        public AudioService(DiscordShardedClient client)
        {
            _client = client;
        }

        public async Task StartAsync()
        {
            ulong guildId = 463430274823356417;
            ulong channelId = 463471372589465622;

            var guild = _client.GetGuild(guildId);
            var voiceChannel = (IVoiceChannel)guild.GetChannel(channelId);

            var sessionIdTsc = new TaskCompletionSource<string>();
            var socketVoiceServerTsc = new TaskCompletionSource<SocketVoiceServer>();

            _client.UserVoiceStateUpdated += VoiceStateUpdatedAsync;
            _client.VoiceServerUpdated += VoiceServerUpdatedAsync;

            Task VoiceStateUpdatedAsync(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
            {
                if (user.Id != _client.CurrentUser.Id || string.IsNullOrWhiteSpace(newState.VoiceSessionId))
                    return Task.CompletedTask;

                sessionIdTsc.TrySetResult(newState.VoiceSessionId);

                return Task.CompletedTask;
            }

            Task VoiceServerUpdatedAsync(SocketVoiceServer arg)
            {
                if (arg.Guild.Id == guildId)
                {
                    socketVoiceServerTsc.TrySetResult(arg);
                }

                return Task.CompletedTask;
            }

            await voiceChannel.ConnectAsync(external: true);
            var sessionId = await sessionIdTsc.Task;
            var voiceServer = await socketVoiceServerTsc.Task;


            await voiceChannel.DisconnectAsync();

            _client.UserVoiceStateUpdated -= VoiceStateUpdatedAsync;
            _client.VoiceServerUpdated -= VoiceServerUpdatedAsync;
        }

        private Process CreateStream(string path)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
        }
    }
}