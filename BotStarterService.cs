using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests
{
    public class BotStarterService : IHostedService
    {
        private const string TokenEnvName = "botToken";

        private readonly IConfiguration _configuration;
        private readonly DiscordShardedClient _client;

        public BotStarterService(IConfiguration configuration, DiscordShardedClient client)
        {
            _configuration = configuration;
            _client = client;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var token = _configuration.GetSection(TokenEnvName).Value;

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _client.SetStatusAsync(UserStatus.Invisible);
            await _client.StopAsync();
        }
    }
}
