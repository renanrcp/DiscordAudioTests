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

namespace DiscordAudioTests
{
    public class AudioService
    {
        public class Payload
        {
            public Payload()
            {
            }

            public Payload(int op)
            {
                this.op = op;
            }

            public int op { get; set; }
        }

        public class Payload<T> : Payload
        {
            public Payload()
            {
            }

            public Payload(int op, T d) : base(op)
            {
                this.d = d;
            }

            public T d { get; set; }
        }

        public class VoiceIdentifyPayload
        {
            public VoiceIdentifyPayload()
            {
            }

            public VoiceIdentifyPayload(ulong server_id, ulong user_id, string session_id, string token)
            {
                this.server_id = server_id;
                this.user_id = user_id;
                this.session_id = session_id;
                this.token = token;
            }

            public ulong server_id { get; set; }
            public ulong user_id { get; set; }
            public string session_id { get; set; }
            public string token { get; set; }
        };

        public class VoiceReadyPayload
        {
            public int ssrc { get; set; }

            public string ip { get; set; }

            public int port { get; set; }

            public string[] modes { get; set; }
        }

        public class HelloPayload
        {
            public HelloPayload()
            {
            }

            public HelloPayload(float heartbeat_interval)
            {
                this.heartbeat_interval = heartbeat_interval;
            }

            public float heartbeat_interval { get; set; }
        }

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

            var webSocket = new ClientWebSocket();

            var connectionUri = new Uri($"wss://{voiceServer.Endpoint}?v=4");

            await webSocket.ConnectAsync(connectionUri, default);

            var helloPayloadBytes = new byte[1024].AsMemory();

            var helloResult = await webSocket.ReceiveAsync(helloPayloadBytes, default);

            helloPayloadBytes = helloPayloadBytes.Slice(0, helloResult.Count);

            var helloPayload = JsonSerializer.Deserialize<Payload<HelloPayload>>(helloPayloadBytes.Span);

            var voiceIdentifyPayload = new Payload<VoiceIdentifyPayload>(0, new VoiceIdentifyPayload(guildId, _client.CurrentUser.Id, sessionId, voiceServer.Token));

            var voiceIdentifyPayloadJson = JsonSerializer.SerializeToUtf8Bytes(voiceIdentifyPayload);

            await webSocket.SendAsync(voiceIdentifyPayloadJson, WebSocketMessageType.Text, true, default);

            var voiceReadyPayloadBytes = new byte[1024].AsMemory();

            var voiceIdentityResult = await webSocket.ReceiveAsync(voiceReadyPayloadBytes, default);

            voiceReadyPayloadBytes = voiceReadyPayloadBytes.Slice(0, voiceIdentityResult.Count);

            var voiceReadyPayload = JsonSerializer.Deserialize<Payload<VoiceReadyPayload>>(voiceReadyPayloadBytes.Span);

            var heartBeatPayload = new Payload<float>(3, helloPayload.d.heartbeat_interval);

            var heartBeatPayloadJson = JsonSerializer.SerializeToUtf8Bytes(heartBeatPayload);

            await webSocket.SendAsync(heartBeatPayloadJson, WebSocketMessageType.Text, true, default);

            await voiceChannel.DisconnectAsync();

            _client.UserVoiceStateUpdated -= VoiceStateUpdatedAsync;
            _client.VoiceServerUpdated -= VoiceServerUpdatedAsync;
        }

        private Process CreateFFmpegStream(string path)
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