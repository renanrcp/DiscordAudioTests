// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordAudioTests.Websockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YoutubeExplode;

namespace DiscordAudioTests;

public class Startup
{
    public Startup(IHostEnvironment environment, IConfiguration configuration)
    {
        Environment = environment;
        Configuration = configuration;
    }

    public IHostEnvironment Environment { get; }

    public IConfiguration Configuration { get; }

    public static void ConfigureServices(IServiceCollection services)
    {
        _ = services.AddSingleton(new DiscordSocketConfig
        {
            AlwaysDownloadUsers = false,
        });
        _ = services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<DiscordSocketConfig>();

            return new DiscordShardedClient(config);
        });
        _ = services.AddSingleton(new CommandServiceConfig
        {
            CaseSensitiveCommands = true,
            IgnoreExtraArgs = true,
            DefaultRunMode = RunMode.Async,
            SeparatorChar = ' ',
            LogLevel = LogSeverity.Debug,
        });
        _ = services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<CommandServiceConfig>();

            return new CommandService(config);
        });

        services.AddCustomHttpClient();

        _ = services.AddSingleton(sp =>
        {
            var client = sp.GetRequiredService<HttpClient>();
            return new YoutubeClient(client);
        });
        _ = services.AddSingleton<AudioService>();
        _ = services.AddTransient<VoiceGatewayClientFactory>();

        _ = services.AddHostedService<BotLogService>();
        _ = services.AddHostedService<BotStarterService>();
    }
}

public static class HttpExtentions
{
    public static void AddCustomHttpClient(this IServiceCollection services)
    {
        _ = services.AddSingleton(sp =>
        {
            var handler = new SocketsHttpHandler
            {
                ConnectCallback = ConnectAsync,
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };

            return handler;
        });

        _ = services.AddSingleton(sp =>
        {
            var handler = sp.GetRequiredService<SocketsHttpHandler>();

            var client = new HttpClient(handler);

            return client;
        });
    }

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        // Use DNS to look up the IP addresses of the target host:
        // - IP v4: AddressFamily.InterNetwork
        // - IP v6: AddressFamily.InterNetworkV6
        // - IP v4 or IP v6: AddressFamily.Unspecified
        // note: this method throws a SocketException when there is no IP address for the host
        var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, cancellationToken);

        // Open the connection to the target host/port
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        {
            // Turn off Nagle's algorithm since it degrades performance in most HttpClient scenarios.
            NoDelay = true
        };

        try
        {
            await socket.ConnectAsync(entry.AddressList, context.DnsEndPoint.Port, cancellationToken);

            // If you want to choose a specific IP address to connect to the server
            // await socket.ConnectAsync(
            //    entry.AddressList[Random.Shared.Next(0, entry.AddressList.Length)],
            //    context.DnsEndPoint.Port, cancellationToken);

            // Return the NetworkStream to the caller
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
