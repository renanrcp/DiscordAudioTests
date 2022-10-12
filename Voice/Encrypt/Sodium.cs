// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

// This file is part of the DSharpPlus project.
//
// Copyright (c) 2015 Mike Santiago
// Copyright (c) 2016-2022 DSharpPlus Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace DiscordAudioTests.Voice.Encrypt;

public static class Sodium
{
    private const string LibraryName = "libsodium";

    public static int KeySize { get; } = (int)_SodiumSecretBoxKeySize();

    public static int NonceSize { get; } = (int)_SodiumSecretBoxNonceSize();

    public static int MacSize { get; } = (int)_SodiumSecretBoxMacSize();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "crypto_secretbox_xsalsa20poly1305_keybytes")]
    [return: MarshalAs(UnmanagedType.SysUInt)]
    private static extern UIntPtr _SodiumSecretBoxKeySize();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "crypto_secretbox_xsalsa20poly1305_noncebytes")]
    [return: MarshalAs(UnmanagedType.SysUInt)]
    private static extern UIntPtr _SodiumSecretBoxNonceSize();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "crypto_secretbox_xsalsa20poly1305_macbytes")]
    [return: MarshalAs(UnmanagedType.SysUInt)]
    private static extern UIntPtr _SodiumSecretBoxMacSize();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "crypto_secretbox_easy")]
    private static extern unsafe int _SodiumSecretBoxCreate(byte* buffer, byte* message, ulong messageLength, byte* nonce, byte* key);

    public static int CalculateTargetSize(ReadOnlySpan<byte> source)
    {
        return source.Length + MacSize;
    }

    public static void Encrypt(ReadOnlySpan<byte> source, Span<byte> target, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> key)
    {
        if (nonce.Length != NonceSize)
        {
            throw new ArgumentException($"Invalid nonce size. Nonce needs to have a length of {NonceSize} bytes.", nameof(nonce));
        }

        if (target.Length != MacSize + source.Length)
        {
            throw new ArgumentException($"Invalid target buffer size. Target buffer needs to have a length that is a sum of input buffer length and Sodium MAC size ({MacSize} bytes).", nameof(target));
        }

        int result;

        if ((result = EncryptInternal(source, target, key, nonce)) != 0)
        {
            throw new CryptographicException($"Could not encrypt the buffer. Sodium returned code {result}.");
        }
    }

    private static unsafe int EncryptInternal(ReadOnlySpan<byte> source, Span<byte> target, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce)
    {
        var status = 0;
        fixed (byte* sourcePtr = &source.GetPinnableReference())
        fixed (byte* targetPtr = &target.GetPinnableReference())
        fixed (byte* keyPtr = &key.GetPinnableReference())
        fixed (byte* noncePtr = &nonce.GetPinnableReference())
        {
            status = _SodiumSecretBoxCreate(targetPtr, sourcePtr, (ulong)source.Length, noncePtr, keyPtr);
        }

        return status;
    }
}
