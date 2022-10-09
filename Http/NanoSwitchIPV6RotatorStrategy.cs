// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net;
using System.Numerics;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests.Http;

public sealed class NanoSwitchIPV6RotatorStrategy : IPV6RotatorStrategy
{
    private readonly BigInteger startTime = GetNanoTime();

    public NanoSwitchIPV6RotatorStrategy(
        string ipv6Block,
        ILogger<NanoSwitchIPV6RotatorStrategy> logger,
        TimeSpan? failedCacheTime = null) : base(ipv6Block, logger, failedCacheTime)
    {
    }

    public override IPAddress GetIPAddress()
    {
        var now = GetNanoTime();
        var nanoOffset = now - startTime;

        return IPAddresses[nanoOffset];
    }

    private static BigInteger GetNanoTime()
    {
        var nano = 10000L * Stopwatch.GetTimestamp();
        nano /= TimeSpan.TicksPerMillisecond;
        nano *= 100L;
        return nano;
    }
}
