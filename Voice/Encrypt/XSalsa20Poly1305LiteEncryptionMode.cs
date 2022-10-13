// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;

namespace DiscordAudioTests.Voice.Encrypt;

public static class XSalsa20Poly1305LiteEncryptionMode
{
    public static int CalculatePacketSize(int frameLength)
    {
        return Sodium.CalculateTargetSize(frameLength) + Rtp.HeaderSize + 4;
    }

    public static void GenerateNonce(Span<byte> nonce, uint seq)
    {
        BinaryPrimitives.WriteUInt32BigEndian(nonce, seq);
    }
}
