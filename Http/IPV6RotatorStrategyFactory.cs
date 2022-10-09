// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests.Http;

public class IPV6RotatorStrategyFactory
{
    public const string IPV6BlockEnvName = "IPV6Block";

    private readonly ConcurrentDictionary<string, Lazy<IPV6RotatorStrategy>> _strategies = new();

    public IPV6RotatorStrategyFactory()
    {
    }


    public IPV6RotatorStrategy GetForName<TRotator>(IServiceProvider serviceProvider, string name)
        where TRotator : IPV6RotatorStrategy
    {
        return _strategies.GetOrAdd(name, CreateStrategy<TRotator>(serviceProvider)).Value;
    }

    private static Lazy<IPV6RotatorStrategy> CreateStrategy<TRotator>(IServiceProvider serviceProvider)
        where TRotator : IPV6RotatorStrategy
    {
        return new Lazy<IPV6RotatorStrategy>(() =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<TRotator>>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            var ipv6block = configuration.GetSection(IPV6BlockEnvName).Value;

            return ActivatorUtilities.CreateInstance<TRotator>(serviceProvider, ipv6block, logger);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }
}
