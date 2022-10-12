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
        ["xsalsa20_poly1305_lite"] = EncryptionMode.XSalsa20Poly1305LiteEncryptionMode,
        ["xsalsa20_poly1305_suffix"] = EncryptionMode.XSalsa20Poly1305SuffixEncryptionMode,
        ["xsalsa20_poly1305"] = EncryptionMode.XSalsa20Poly1305EncryptionMode,
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

    public static int CalculatePacketSize(this EncryptionMode mode)
    {
        return CalculatePacketSize(mode, Rtp.GetEncryptionLength());
    }

    public static int CalculatePacketSize(this EncryptionMode mode, int encryptionLength)
    {
        return mode switch
        {
            EncryptionMode.XSalsa20Poly1305EncryptionMode => XSalsa20Poly1305EncryptionMode.CalculatePacketSize(encryptionLength),
            EncryptionMode.XSalsa20Poly1305LiteEncryptionMode => XSalsa20Poly1305LiteEncryptionMode.CalculatePacketSize(encryptionLength),
            EncryptionMode.XSalsa20Poly1305SuffixEncryptionMode => XSalsa20Poly1305SuffixEncryptionMode.CalculatePacketSize(encryptionLength),
            _ => 0,
        };
    }

    public static void AppendNonce(this EncryptionMode mode, ReadOnlySpan<byte> nonce, Span<byte> target)
    {
        switch (mode)
        {
            case EncryptionMode.XSalsa20Poly1305EncryptionMode:
                break;
            case EncryptionMode.XSalsa20Poly1305LiteEncryptionMode:
                XSalsa20Poly1305LiteEncryptionMode.AppendNonce(nonce, target);
                break;
            case EncryptionMode.XSalsa20Poly1305SuffixEncryptionMode:
                XSalsa20Poly1305SuffixEncryptionMode.AppendNonce(nonce, target);
                break;
            default:
                break;
        }
    }

    public static void GenerateNonce(this EncryptionMode mode, ReadOnlySpan<byte> rtpHeader, uint nonce, Span<byte> target)
    {
        switch (mode)
        {
            case EncryptionMode.XSalsa20Poly1305EncryptionMode:
                XSalsa20Poly1305EncryptionMode.GenerateNonce(rtpHeader, target);
                break;
            case EncryptionMode.XSalsa20Poly1305LiteEncryptionMode:
                XSalsa20Poly1305LiteEncryptionMode.GenerateNonce(nonce, target);
                break;
            case EncryptionMode.XSalsa20Poly1305SuffixEncryptionMode:
                XSalsa20Poly1305SuffixEncryptionMode.GenerateNonce(target);
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
