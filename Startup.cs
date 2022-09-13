// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordAudioTests.Websockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YoutubeExplode;

namespace DiscordAudioTests
{
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


            _ = services.AddSingleton<AudioService>();
            _ = services.AddSingleton<YoutubeClient>();
            _ = services.AddTransient<VoiceGatewayClientFactory>();

            _ = services.AddHostedService<BotLogService>();
            _ = services.AddHostedService<BotStarterService>();
        }
    }
}
