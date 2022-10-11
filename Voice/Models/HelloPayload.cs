// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace DiscordAudioTests.Voice.Models;

public class HelloPayload
{
    [JsonPropertyName("heartbeat_interval")]
    public float HeartbeatInterval { get; set; }
}
