// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using YoutubeExplode;

namespace DiscordAudioTests.Http;

public static class HttpDependencyInjectionExtensions
{
    public static IHttpClientBuilder AddYoutubeClient(this IServiceCollection services)
    {
        return services.AddHttpClient<YoutubeClient>()
                        .ConfigurePrimaryHttpMessageHandler(() =>
                        {
                            return new SocketsHttpHandler()
                            {
                                UseCookies = false,
                                ConnectCallback = async (context, cancellationToken) =>
                                {
                                    var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, cancellationToken);

                                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
                                    {
                                        NoDelay = true
                                    };

                                    try
                                    {
                                        await socket.ConnectAsync(entry.AddressList, context.DnsEndPoint.Port, cancellationToken);

                                        return new NetworkStream(socket, ownsSocket: true);
                                    }
                                    catch
                                    {
                                        socket.Dispose();
                                        throw;
                                    }
                                },
                                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                            };
                        })
                        .AddPolicyHandler(GetAsyncRetryPolicy());
    }

    public static IHttpClientBuilder AddYoutubeClientWithIPV6Rotator<TRotator>(this IServiceCollection services)
        where TRotator : IPV6RotatorStrategy
    {
        return services
                .AddHttpClientWithIPV6Rotator<YoutubeClient, TRotator>((response) => response.StatusCode == HttpStatusCode.TooManyRequests)
                .AddPolicyHandler(GetAsyncRetryPolicy());
    }

    public static IHttpClientBuilder AddHttpClientWithIPV6Rotator<TClient, TRotator>(
        this IServiceCollection services,
        Func<HttpResponseMessage, bool> banIpCheck)
        where TClient : class
        where TRotator : IPV6RotatorStrategy
    {
        services.TryAddSingleton<IPV6RotatorStrategyFactory>();
        var builder = services.AddHttpClient<TClient>();

        builder = builder.ConfigurePrimaryHttpMessageHandler((sp) =>
                    {
                        var socketsHttpHandler = new SocketsHttpHandler()
                        {
                            UseCookies = false,
                            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                        };

                        var iPV6RotatorStrategyFactory = sp.GetRequiredService<IPV6RotatorStrategyFactory>();
                        var logger = sp.GetRequiredService<ILogger<TRotator>>();

                        var iPV6RotatorStrategy = iPV6RotatorStrategyFactory.GetForName<TRotator>(sp, builder.Name);

                        socketsHttpHandler.ConfigureIPV6RotatorStrategy(iPV6RotatorStrategy, logger);

                        return socketsHttpHandler;
                    })
                    .AddHttpMessageHandler((sp) =>
                    {
                        var iPV6RotatorStrategyFactory = sp.GetRequiredService<IPV6RotatorStrategyFactory>();

                        var iPV6RotatorStrategy = iPV6RotatorStrategyFactory.GetForName<TRotator>(sp, builder.Name);

                        return new IPV6RotatorHttpHandler(iPV6RotatorStrategy, banIpCheck);
                    });

        return builder;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetAsyncRetryPolicy()
    {
        return HttpPolicyExtensions.HandleTransientHttpError().RetryAsync(2);
    }
}
