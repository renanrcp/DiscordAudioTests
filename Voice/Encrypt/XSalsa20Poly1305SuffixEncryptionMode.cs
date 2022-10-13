// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;

namespace DiscordAudioTests.Voice.Encrypt;

public static class XSalsa20Poly1305SuffixEncryptionMode
{
    public static int CalculatePacketSize(int frameLength)
    {
        return Sodium.CalculateTargetSize(frameLength) + Rtp.HeaderSize + Sodium.NonceSize;
    }

    public static void GenerateNonce(Span<byte> nonce)
    {
        RandomNumberGenerator.Fill(nonce);
    }
}
