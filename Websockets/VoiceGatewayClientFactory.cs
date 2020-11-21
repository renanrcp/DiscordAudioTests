using System.Threading;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;

namespace DiscordAudioTests.Websockets
{
    public class VoiceGatewayClientFactory
    {
        private readonly DiscordShardedClient _client;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly CancellationToken _cts;

        public VoiceGatewayClientFactory(DiscordShardedClient client, IHostApplicationLifetime lifetime)
        {
            _client = client;
            _lifetime = lifetime;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.ApplicationStopping).Token;
        }

        public VoiceGatewayClient Create(SocketVoiceServer voiceServer, string sessionId)
        {
            var connectionInfo = new ConnectionInfo(voiceServer, _client.CurrentUser.Id, sessionId);

            return new(connectionInfo, _cts);
        }
    }
}