// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Linq;

namespace DiscordAudioTests.Voice.Models;

public static class OpcodeExtensions
{
    private static readonly int[] PayloadOpcodeValues = Enum.GetValues(typeof(PayloadOpcode)).Cast<int>().ToArray();

    public static bool TryParsePayloadOpcode(this int opcodeRaw, out PayloadOpcode payloadOpcode)
    {
        payloadOpcode = PayloadOpcode.Unknown;

        if (!PayloadOpcodeValues.Contains(opcodeRaw))
        {
            return false;
        }

        payloadOpcode = (PayloadOpcode)opcodeRaw;
        return true;
    }
}
