// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Text.Json.Serialization;

namespace DiscordAudioTests.Voice.Models;

public class SelectProtocolPayload
{
    public SelectProtocolPayload(string protocol, ProtocolData data)
    {
        Protocol = protocol;
        Data = data;
    }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; }

    [JsonPropertyName("data")]
    public ProtocolData Data { get; set; }

    [JsonPropertyName("rtc_connection_id")]
    public string RtcConnectionId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("codecs")]
    public Codec[] Codecs { get; set; } = new Codec[]
    {
        new("opus", 120, 1000, "audio"),
    };

    public class ProtocolData
    {
        public ProtocolData()
        {
        }

        public ProtocolData(string address, ushort port, string mode)
        {
            Address = address;
            Port = port;
            Mode = mode;
        }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("port")]
        public ushort Port { get; set; }

        [JsonPropertyName("mode")]
        public string Mode { get; set; }
    }

    public class Codec
    {
        public Codec()
        {
        }

        public Codec(string name, byte payloadType, int priority, string type)
        {
            Name = name;
            PayloadType = payloadType;
            Priority = priority;
            Type = type;
        }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("payload_type")]
        public byte PayloadType { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }
}
