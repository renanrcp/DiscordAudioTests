using Discord.WebSocket;

namespace DiscordAudioTests.Websockets
{
    public class ConnectionInfo
    {
        public ConnectionInfo(SocketVoiceServer voiceServer, ulong userId, string sessionId)
        {
            GuildId = voiceServer.Guild.Id;
            UserId = userId;
            Token = voiceServer.Token;
            Endpoint = voiceServer.Endpoint;
            SessionId = sessionId;
        }

        public ulong GuildId { get; }

        public ulong UserId { get; }

        public string Token { get; }

        public string Endpoint { get; }

        public string SessionId { get; set; }
    }
}