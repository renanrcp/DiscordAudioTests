// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace DiscordAudioTests.Voice.Models;

public class ReadyPayload
{
    [JsonPropertyName("ssrc")]
    public uint Ssrc { get; set; }

    [JsonPropertyName("ip")]
    public string Ip { get; set; }

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("modes")]
    public string[] Modes { get; set; }
}
