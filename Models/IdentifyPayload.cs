using System.Text.Json.Serialization;
using DiscordAudioTests.Websockets;

namespace DiscordAudioTests.Models
{
    public class IdentifyPayload
    {
        public IdentifyPayload()
        {
        }

        public IdentifyPayload(ConnectionInfo connectionInfo)
        {
            GuildId = connectionInfo.GuildId;
            UserId = connectionInfo.UserId;
            SessionId = connectionInfo.SessionId;
            Token = connectionInfo.Token;
        }

        [JsonPropertyName("server_id")]
        public ulong GuildId { get; set; }

        [JsonPropertyName("user_id")]
        public ulong UserId { get; set; }

        [JsonPropertyName("session_id")]
        public string SessionId { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }
    }
}