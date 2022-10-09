// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Numerics;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests.Http;

public sealed class RotateOnBanIPV6RotatorStrategy : IPV6RotatorStrategy
{
    private readonly object _indexLock = new();
    private readonly object _currentBlockLock = new();

    private BigInteger _index;
    private BigInteger _currentBlock;

    public RotateOnBanIPV6RotatorStrategy(
        string ipv6Block,
        ILogger<RotateOnBanIPV6RotatorStrategy> logger,
        TimeSpan? failedCacheTime = null) : base(ipv6Block, logger, failedCacheTime)
    {
    }

    private BigInteger Index
    {
        get
        {
            lock (_indexLock)
            {
                return _index;
            }
        }
        set
        {
            lock (_indexLock)
            {
                _index = value;
            }
        }
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

    public override IPAddress GetIPAddress()
    {
        IPAddress ipAddress;
        long triesSinceBlockSkip = 0;
        var it = BigInteger.Zero;
        do
        {
            if (IPAddresses.Count * 2 < it)
            {
                throw new InvalidOperationException("Cannot find a free ip");
            }

            if (IPAddresses.Count > new BigInteger(128))
            {
                Index += new BigInteger(Random.Shared.Next(10) + 10);
            }
            else
            {
                Index += 1;
            }

            it += 1;

            triesSinceBlockSkip++;

            if (IPAddresses.Count > BLOCK64SIZE && triesSinceBlockSkip > 128)
            {
                triesSinceBlockSkip = 0;
                CurrentBlock += 1;
            }

            var blockOffset = CurrentBlock * BLOCK64SIZE;

            try
            {
                ipAddress = IPAddresses[blockOffset + Index - 1];
            }
            catch
            {
                Index = 0;
                ipAddress = null;
            }

        } while (ipAddress == null || !IsValidAddress(ipAddress));

        return ipAddress;
    }
}
