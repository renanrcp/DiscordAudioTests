using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests
{
    public class BotLogService : BackgroundService
    {
        private const string BASE_CATEGORY_NAME = "Discord.Net";

        private readonly DiscordShardedClient _client;
        private readonly ILoggerFactory _loggerFactory;

        public BotLogService(DiscordShardedClient client, ILoggerFactory loggerFactory)
        {
            _client = client;
            _loggerFactory = loggerFactory;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _client.Log += LogAsync;

            return Task.CompletedTask;
        }

        private Task LogAsync(LogMessage arg)
        {
            try
            {
                var logger = _loggerFactory.CreateLogger($"{BASE_CATEGORY_NAME}.{arg.Source}");

                var logLevel = (LogLevel)(Math.Abs((int)arg.Severity - 5));

                logger.Log(logLevel, default, arg.Exception, arg.Message);
            }
            catch { }

            return Task.CompletedTask;
        }
    }
}