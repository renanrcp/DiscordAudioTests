// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;

namespace DiscordAudioTests.Voice.Encrypt;

public static class XSalsa20Poly1305LiteEncryptionMode
{
    public static int CalculatePacketSize(int encryptionLength)
    {
        return Rtp.HeaderSize + encryptionLength + 4;
    }

    public static void GenerateNonce(uint nonce, Span<byte> target)
    {
        if (target.Length != Sodium.NonceSize)
        {
            throw new ArgumentException($"Invalid nonce buffer size. Target buffer for the nonce needs to have a capacity of {Sodium.NonceSize} bytes.", nameof(target));
        }

        // Write the uint to memory
        BinaryPrimitives.WriteUInt32BigEndian(target, nonce);

        // Zero rest of the buffer.
        target[4..].Fill(0);
    }

    public static void AppendNonce(ReadOnlySpan<byte> nonce, Span<byte> target)
    {
        nonce[..4].CopyTo(target[^4..]);
    }
}
