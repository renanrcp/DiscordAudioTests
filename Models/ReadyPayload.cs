using System.Text.Json.Serialization;

namespace DiscordAudioTests.Models
{
    public class ReadyPayload
    {
        [JsonPropertyName("ssrc")]
        public int Ssrc { get; set; }

        [JsonPropertyName("ip")]
        public string Ip { get; set; }

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("modes")]
        public string[] Modes { get; set; }
    }
}