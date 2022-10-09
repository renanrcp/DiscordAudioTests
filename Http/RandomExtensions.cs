// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Numerics;

namespace DiscordAudioTests.Http;

public static class RandomExtensions
{
    public static BigInteger NextBigInteger(this Random random, BigInteger minValue, BigInteger maxValue)
    {
        if (minValue > maxValue)
        {
            throw new InvalidOperationException();
        }

        if (minValue == maxValue)
        {
            return minValue;
        }

        var zeroBasedUpperBound = maxValue - 1 - minValue; // Inclusive
        Debug.Assert(zeroBasedUpperBound.Sign >= 0);
        var bytes = zeroBasedUpperBound.ToByteArray();
        Debug.Assert(bytes.Length > 0);
        Debug.Assert((bytes[^1] & 0b10000000) == 0);

        // Search for the most significant non-zero bit
        byte lastByteMask = 0b11111111;
        for (byte mask = 0b10000000; mask > 0; mask >>= 1, lastByteMask >>= 1)
        {
            if ((bytes[^1] & mask) == mask)
            {
                break; // We found it
            }
        }

        while (true)
        {
            random.NextBytes(bytes);
            bytes[^1] &= lastByteMask;
            var result = new BigInteger(bytes);
            Debug.Assert(result.Sign >= 0);
            if (result <= zeroBasedUpperBound)
            {
                return result + minValue;
            }
        }
    }
}
