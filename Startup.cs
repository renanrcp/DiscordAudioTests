using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(new DiscordSocketConfig
            {
                AlwaysDownloadUsers = false,
            });
            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<DiscordSocketConfig>();

                return new DiscordShardedClient(config);
            });
            services.AddSingleton<AudioService>();

            services.AddHostedService<BotLogService>();
            services.AddHostedService<BotStarterService>();
        }
    }
}