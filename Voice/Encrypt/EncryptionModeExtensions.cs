// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace DiscordAudioTests.Voice.Encrypt;

public static class EncryptionModeExtensions
{
    private static Dictionary<string, EncryptionMode> SupportedModes => new()
    {
        ["xsalsa20_poly1305_lite"] = EncryptionMode.XSalsa20Poly1305Lite,
        ["xsalsa20_poly1305_suffix"] = EncryptionMode.XSalsa20Poly1305Suffix,
        ["xsalsa20_poly1305"] = EncryptionMode.XSalsa20Poly1305,
    };

    public static string SelectMode(string[] modes)
    {
        foreach (var mode in modes)
        {
            if (SupportedModes.ContainsKey(mode))
            {
                return mode;
            }
        }

        throw new CryptographicException("Could not negotiate Sodium encryption modes, as none of the modes offered by Discord are supported.");
    }

    public static EncryptionMode GetEncryptionMode(string mode)
    {
        return SupportedModes[mode];
    }

#pragma warning disable IDE0060
    public static int GetPacketLength(this EncryptionMode mode, int frameLength)
    {
        return Sodium.CalculateTargetSize(frameLength) + Rtp.HeaderSize + Sodium.NonceSize;
    }
#pragma warning restore IDE0060

    public static int CalculatePacketSize(this EncryptionMode mode, int frameLength)
    {
        return mode switch
        {
            EncryptionMode.XSalsa20Poly1305 => XSalsa20Poly1305EncryptionMode.CalculatePacketSize(frameLength),
            EncryptionMode.XSalsa20Poly1305Lite => XSalsa20Poly1305LiteEncryptionMode.CalculatePacketSize(frameLength),
            EncryptionMode.XSalsa20Poly1305Suffix => XSalsa20Poly1305SuffixEncryptionMode.CalculatePacketSize(frameLength),
            _ => 0,
        };
    }

    public static void GenerateNonce(this EncryptionMode mode, Span<byte> nonce, ReadOnlySpan<byte> rtpHeader, uint seq)
    {
        if (nonce.Length != Sodium.NonceSize)
        {
            throw new ArgumentException($"'{nameof(nonce)}' cannot have length different than '{Sodium.NonceSize}'.", nameof(nonce));
        }

        switch (mode)
        {
            case EncryptionMode.XSalsa20Poly1305:
                XSalsa20Poly1305EncryptionMode.GenerateNonce(nonce, rtpHeader);
                break;
            case EncryptionMode.XSalsa20Poly1305Lite:
                XSalsa20Poly1305LiteEncryptionMode.GenerateNonce(nonce, seq);
                break;
            case EncryptionMode.XSalsa20Poly1305Suffix:
                XSalsa20Poly1305SuffixEncryptionMode.GenerateNonce(nonce);
                break;
            default:
                break;
        }
    }

#pragma warning disable IDE0060
    public static void Encrypt(this EncryptionMode mode, ReadOnlySpan<byte> source, Span<byte> target, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> key)
    {
        Sodium.Encrypt(source, target, nonce, key);
    }
#pragma warning restore IDE0060
}
