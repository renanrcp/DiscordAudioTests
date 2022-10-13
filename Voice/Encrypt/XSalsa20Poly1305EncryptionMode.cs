// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;

namespace DiscordAudioTests.Voice.Encrypt;

public static class XSalsa20Poly1305EncryptionMode
{
    public static int CalculatePacketSize(int frameLength)
    {
        return Sodium.CalculateTargetSize(frameLength) + Rtp.HeaderSize;
    }

    public static void GenerateNonce(Span<byte> nonce, ReadOnlySpan<byte> rtpHeader)
    {
        rtpHeader.CopyTo(nonce);
    }
}
