// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests;

public class BotLogService : BackgroundService
{
    private const string BASE_CATEGORY_NAME = "Discord.Net";

    private readonly DiscordShardedClient _client;
    private readonly CommandService _commandService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, ILogger> _loggers = new();

    public BotLogService(DiscordShardedClient client, CommandService commandService, ILoggerFactory loggerFactory)
    {
        _client = client;
        _loggerFactory = loggerFactory;
        _commandService = commandService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.Log += LogAsync;
        _commandService.Log += LogAsync;

        return Task.CompletedTask;
    }

    private Task LogAsync(LogMessage arg)
    {
        try
        {
            var categoryName = $"{BASE_CATEGORY_NAME}.{arg.Source}";

            if (!_loggers.TryGetValue(categoryName, out var logger))
            {
                logger = _loggerFactory.CreateLogger(categoryName);
                _ = _loggers.TryAdd(categoryName, logger);
            }

            var logLevel = (LogLevel)Math.Abs((int)arg.Severity - 5);

            logger.Log(logLevel, default, arg.Exception?.InnerException ?? arg.Exception, arg.Message);
        }
        catch { }

        return Task.CompletedTask;
    }
}
