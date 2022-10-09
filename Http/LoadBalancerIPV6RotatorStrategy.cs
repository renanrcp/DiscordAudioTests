// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Numerics;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests.Http;

public sealed class LoadBalancerIPV6RotatorStrategy : IPV6RotatorStrategy
{
    public LoadBalancerIPV6RotatorStrategy(
        string ipv6Block,
        ILogger<LoadBalancerIPV6RotatorStrategy> logger,
        TimeSpan? failedCacheTime = null) : base(ipv6Block, logger, failedCacheTime)
    {
    }

    public override IPAddress GetIPAddress()
    {
        IPAddress ipAddress;
        var it = BigInteger.Zero;

        do
        {
            if (IPAddresses.Count * new BigInteger(2) < it)
            {
                throw new InvalidOperationException("Cannot find a free ip");
            }

            it += BigInteger.One;

            ipAddress = IPAddresses[Random.Shared.NextBigInteger(0, IPAddresses.Count)];
        } while (ipAddress == null || !IsValidAddress(ipAddress));

        return ipAddress;
    }
}
