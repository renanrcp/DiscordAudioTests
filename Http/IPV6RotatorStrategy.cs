// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests.Http;

public abstract class IPV6RotatorStrategy
{
    private static readonly TimeSpan DEFAULT_FAILED_IP_DURATION = TimeSpan.FromDays(7);
    public static readonly BigInteger BLOCK64SIZE = BigInteger.Pow(2, 64);

    private readonly TimeSpan _failedCacheTime;
    private readonly ConcurrentDictionary<IPAddress, long> _failedAddresses = new();
    private readonly ILogger _logger;


    private IPAddress _lastUsedAddress;

    protected IPV6RotatorStrategy(string ipv6Block, ILogger logger, TimeSpan? failedCacheTime = null)
    {
        IPAddresses = IPNetwork.Parse(ipv6Block).ListIPAddress(FilterEnum.Usable);
        _logger = logger;

        _failedCacheTime = failedCacheTime ?? DEFAULT_FAILED_IP_DURATION;
    }

    public IPAddressCollection IPAddresses { get; }

    public IReadOnlyDictionary<IPAddress, long> FailedAddresses => _failedAddresses;

    public IPAddress LastUsedAddress
    {
        get => Volatile.Read(ref _lastUsedAddress);
        protected set => Volatile.Write(ref _lastUsedAddress, value);
    }

    public void AddFailedAddress(IPAddress address)
    {
        _ = _failedAddresses.TryAdd(address, DateTimeOffset.UtcNow.Add(_failedCacheTime).ToUnixTimeMilliseconds());

        _logger.LogInformation("IP: {address} was marked as failed.", address);

        OnFailedAddress(address);
    }

    public void RemoveFailedAddress(IPAddress address)
    {
        _ = _failedAddresses.TryRemove(address, out _);
    }

    public void ClearFailedAddresses()
    {
        _failedAddresses.Clear();
    }

    protected bool IsValidAddress(IPAddress address)
    {
        if (!_failedAddresses.TryGetValue(address, out var expireDateMs))
        {
            return true;
        }

        if (expireDateMs < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            RemoveFailedAddress(address);
            return true;
        }

        return false;
    }

    protected virtual void OnFailedAddress(IPAddress address)
    {
    }

    public abstract IPAddress GetIPAddress();
}
