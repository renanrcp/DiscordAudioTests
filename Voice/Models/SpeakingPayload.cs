// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace DiscordAudioTests.Voice.Models;

public class SpeakingPayload
{
    public SpeakingPayload()
    {
    }

    public SpeakingPayload(uint ssrc, int speaking)
    {
        Ssrc = ssrc;
        Speaking = speaking;
    }

    [JsonPropertyName("ssrc")]
    public uint Ssrc { get; set; }

    [JsonPropertyName("speaking")]
    public int Speaking { get; set; }

    [JsonPropertyName("delay")]
    public int Delay { get; set; }
}
