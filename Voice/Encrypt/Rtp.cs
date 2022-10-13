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
using System.Buffers.Binary;

namespace DiscordAudioTests.Voice.Encrypt;

public static class Rtp
{
    public const int HeaderSize = 12;
    private const byte RtpNoExtension = 0x80;
    private const byte RtpVersion = 0x78;

    public static int CalculateMaximumFrameSize()
    {
        return 120 * (48000 / 1000);
    }

    public static int SampleCountToSampleSize(int sampleCount)
    {
        return sampleCount * 2 * 2;
    }

    public static int GetPacketLength()
    {
        return SampleCountToSampleSize(CalculateMaximumFrameSize());
    }

    public static void EncodeHeader(ushort sequence, uint timestamp, uint ssrc, Span<byte> target)
    {
        if (target.Length < HeaderSize)
        {
            throw new ArgumentException($"Header buffer needs to have at least '{HeaderSize}' bytes.", nameof(target));
        }

        target[0] = RtpNoExtension;
        target[1] = RtpVersion;

        BinaryPrimitives.WriteUInt16BigEndian(target[2..], sequence);
        BinaryPrimitives.WriteUInt32BigEndian(target[4..], timestamp);
        BinaryPrimitives.WriteUInt32BigEndian(target[8..], ssrc);
    }
}
