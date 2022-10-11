// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DiscordAudioTests.Voice.Models;

public class Payload
{
    public const string OPCODE_PROPERTY_NAME = "op";
    public const string PAYLOAD_PROPERTY_NAME = "d";

    public Payload(PayloadOpcode opcode, object payloadBody)
    {
        Opcode = opcode;
        PayloadBody = payloadBody;
    }

    [JsonPropertyName(OPCODE_PROPERTY_NAME)]
    public PayloadOpcode Opcode { get; set; }

    [JsonPropertyName(PAYLOAD_PROPERTY_NAME)]
    public object PayloadBody { get; set; }
}
