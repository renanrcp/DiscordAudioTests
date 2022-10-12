// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;

namespace DiscordAudioTests.Voice.Encrypt;

public static class XSalsa20Poly1305EncryptionMode
{
    public static int CalculatePacketSize(int encryptionLength)
    {
        return Rtp.HeaderSize + encryptionLength;
    }

    public static void GenerateNonce(ReadOnlySpan<byte> rtpHeader, Span<byte> target)
    {
        if (rtpHeader.Length != Rtp.HeaderSize)
        {
            throw new ArgumentException($"RTP header needs to have a length of exactly {Rtp.HeaderSize} bytes.", nameof(rtpHeader));
        }

        if (target.Length != Sodium.NonceSize)
        {
            throw new ArgumentException($"Invalid nonce buffer size. Target buffer for the nonce needs to have a capacity of {Sodium.NonceSize} bytes.", nameof(target));
        }

        // Write the header to the beginning of the span.
        rtpHeader.CopyTo(target);

        // Zero rest of the span.

        target[rtpHeader.Length..].Fill(0);
    }
}
