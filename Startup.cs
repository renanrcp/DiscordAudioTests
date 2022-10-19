// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordAudioTests.Http;
using DiscordAudioTests.Voice;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

    public void ConfigureServices(IServiceCollection services)
    {
        _ = services.AddSingleton(new DiscordSocketConfig
        {
            AlwaysDownloadUsers = false,
            LogLevel = LogSeverity.Debug,
            GatewayIntents = GatewayIntents.All,
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

        _ = Configuration.GetSection(IPV6RotatorStrategyFactory.IPV6BlockEnvName).Value != null
            ? services.AddYoutubeClientWithIPV6Rotator<LoadBalancerIPV6RotatorStrategy>()
            : services.AddYoutubeClient();

        _ = services.AddSingleton<AudioService>();
        _ = services.AddSingleton<AudioPlayerManager>();
        _ = services.AddSingleton<VoiceGatewayClientManager>();

        _ = services.AddHostedService<BotLogService>();
        _ = services.AddHostedService<BotStarterService>();
    }
}
