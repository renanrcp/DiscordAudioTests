// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests;

public class BotStarterService : IHostedService
{
    private const string TokenEnvName = "botToken";

    private readonly IServiceProvider _provider;
    private readonly IConfiguration _configuration;
    private readonly DiscordShardedClient _client;
    private readonly CommandService _commandService;
    private readonly ILogger<CommandService> _commandServiceLogger;

    public BotStarterService(IServiceProvider provider)
    {
        _provider = provider;
        _configuration = provider.GetRequiredService<IConfiguration>();
        _client = provider.GetRequiredService<DiscordShardedClient>();
        _commandService = provider.GetRequiredService<CommandService>();
        _commandServiceLogger = provider.GetRequiredService<ILogger<CommandService>>();
    }

    private int ShardsInitialized;

    private bool IsStarted { get; set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var token = _configuration.GetSection(TokenEnvName).Value;

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        _client.ShardReady += ShardReadyAsync;
        _commandService.CommandExecuted += CommandExecutedAsync;
    }

    private Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
    {
        if (!command.IsSpecified)
        {
            return Task.CompletedTask;
        }

        var commandName = command.Value.Name;
        var guildId = context.Guild.Id;

        if (result.IsSuccess)
        {
            _commandServiceLogger.LogInformation("Command '{commandName}' executed in guild '{guildId}'.", commandName, guildId);
        }
        else if (result is ExecuteResult executeResult && executeResult.Exception != null)
        {
            _commandServiceLogger.LogError(executeResult.Exception, "Command '{commandName}' executed in guild '{guildId}' throwed an exception", commandName, guildId);
            return context.Channel.SendMessageAsync("Ops, an error ocurried when executing this command.");
        }

        return Task.CompletedTask;
    }

    private Task ShardReadyAsync(DiscordSocketClient arg)
    {
        _ = Interlocked.Increment(ref ShardsInitialized);

        if (!IsStarted && Volatile.Read(ref ShardsInitialized) >= _client.Shards.Count)
        {
            IsStarted = true;
            _ = _commandService.AddModulesAsync(typeof(AudioService).Assembly, _provider);
            _client.MessageReceived += MessageReceived;
        }

        return Task.CompletedTask;
    }

    private async Task MessageReceived(SocketMessage arg)
    {
        // Don't process the command if it was a system message
        if (arg is not SocketUserMessage message)
        {
            return;
        }

        // Create a number to track where the prefix ends and the command begins
        var argPos = 0;

        // Determine if the message is a command based on the prefix and make sure no bots trigger commands
        if (!(message.HasCharPrefix('!', ref argPos) ||
            message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
            message.Author.IsBot)
        {
            return;
        }

        // Create a WebSocket-based command context based on the message
        var context = new ShardedCommandContext(_client, message);

        // Execute the command with the command context we just
        // created, along with the service provider for precondition checks.
        _ = await _commandService.ExecuteAsync(
            context: context,
            argPos: argPos,
            _provider);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _client.ShardReady -= ShardReadyAsync;
        _client.MessageReceived -= MessageReceived;
        _commandService.CommandExecuted -= CommandExecutedAsync;

        await _client.SetStatusAsync(UserStatus.Invisible);
        await _client.StopAsync();
    }
}
