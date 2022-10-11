// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace DiscordAudioTests.Voice.Models;

public class ClientConnectPayload
{
    public ClientConnectPayload(uint audioSsrc)
    {
        AudioSsrc = audioSsrc;
    }

    [JsonPropertyName("audio_ssrc")]
    public uint AudioSsrc { get; set; }

    [JsonPropertyName("video_ssrc")]
    public uint VideoSsrc { get; set; }

    [JsonPropertyName("rtx_ssrc")]
    public uint RtxSsrc { get; set; }
}
