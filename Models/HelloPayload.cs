using System;
using System.Text.Json.Serialization;

namespace DiscordAudioTests.Models
{
    public class HelloPayload
    {
        [JsonPropertyName("heartbeat_interval")]
        public float HeartbeatInterval { get; set; }
    }
}