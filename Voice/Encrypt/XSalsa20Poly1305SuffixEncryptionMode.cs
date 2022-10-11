// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Security.Cryptography;

namespace DiscordAudioTests.Voice.Encrypt;

public static class XSalsa20Poly1305SuffixEncryptionMode
{
    public static int CalculatePacketSize()
    {
        return Rtp.HeaderSize + Rtp.GetEncryptionLength() + Sodium.NonceSize;
    }

    public static void GenerateNonce(Span<byte> target)
    {
        if (target.Length != Sodium.NonceSize)
        {
            throw new ArgumentException($"Invalid nonce buffer size. Target buffer for the nonce needs to have a capacity of {Sodium.NonceSize} bytes.", nameof(target));
        }

        var buffer = ArrayPool<byte>.Shared.Rent(Sodium.NonceSize);

        try
        {
            var bufferSpan = buffer.AsSpan(0, Sodium.NonceSize);

            RandomNumberGenerator.Fill(bufferSpan);

            bufferSpan.CopyTo(target);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static void AppendNonce(ReadOnlySpan<byte> nonce, Span<byte> target)
    {
        nonce.CopyTo(target[^12..]);
    }
}
