// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net;
using System.Numerics;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests.Http;

public sealed class RotatingNanoSwitchIPV6RotatorStrategy : IPV6RotatorStrategy
{
    private readonly object _currentBlockLock = new();
    private readonly object _blockNanoStartLock = new();

    private BigInteger _currentBlock;
    private BigInteger _blockNanoStart;

    public RotatingNanoSwitchIPV6RotatorStrategy(
        string ipv6Block,
        ILogger<RotatingNanoSwitchIPV6RotatorStrategy> logger,
        TimeSpan? failedCacheTime = null) : base(ipv6Block, logger, failedCacheTime)
    {
    }

    private BigInteger CurrentBlock
    {
        get
        {
            lock (_currentBlockLock)
            {
                return _currentBlock;
            }
        }
        set
        {
            lock (_currentBlockLock)
            {
                _currentBlock = value;
            }
        }
    }

    private BigInteger BlockNanoStart
    {
        get
        {
            lock (_blockNanoStartLock)
            {
                return _blockNanoStart;
            }
        }
        set
        {
            lock (_blockNanoStartLock)
            {
                _blockNanoStart = value;
            }
        }
    }

    protected override void OnFailedAddress(IPAddress address)
    {
        base.OnFailedAddress(address);

        CurrentBlock += 1;
        BlockNanoStart = GetNanoTime();
    }

    public override IPAddress GetIPAddress()
    {
        IPAddress ipAddress;

        long triesSinceBlockSkip = 0;
        var it = BigInteger.Zero;


        do
        {
            try
            {
                if (IPAddresses.Count * new BigInteger(2) < it)
                {
                    throw new InvalidOperationException("Cannot find a free ip");
                }

                it += 1;
                triesSinceBlockSkip++;

                if (triesSinceBlockSkip > 128)
                {
                    CurrentBlock += 1;
                }

                var nanoTime = GetNanoTime();
                var timeOffset = nanoTime - BlockNanoStart;
                var blockOffset = CurrentBlock * BLOCK64SIZE;
                ipAddress = IPAddresses[blockOffset + timeOffset];
            }
            catch
            {
                CurrentBlock = 0;
                ipAddress = null;
            }
        } while (ipAddress == null || !IsValidAddress(ipAddress));
        return ipAddress;
    }

    private static BigInteger GetNanoTime()
    {
        var nano = 10000L * Stopwatch.GetTimestamp();
        nano /= TimeSpan.TicksPerMillisecond;
        nano *= 100L;
        return nano;
    }
}
