// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace DiscordAudioTests.Voice.Models;

public class IdentifyPayload
{
    public IdentifyPayload()
    {
    }

    public IdentifyPayload(ulong guildId, ulong userId, string sessionId, string token)
    {
        GuildId = guildId;
        UserId = userId;
        SessionId = sessionId;
        Token = token;
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
