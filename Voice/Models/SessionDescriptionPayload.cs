// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace DiscordAudioTests.Voice.Models;

public class SessionDescriptionPayload
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; }

    [JsonPropertyName("secret_key")]
    public byte[] SecretKey { get; set; }
}
