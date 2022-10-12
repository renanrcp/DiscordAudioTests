// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordAudioTests.Voice.Gateway;
using DiscordAudioTests.Voice.Poolers;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests.Voice;

public sealed class VoiceGatewayClientManager : IAsyncDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly DiscordShardedClient _client;
    private readonly ConcurrentDictionary<ulong, VoiceGatewayClient> _voiceClients = new();

    public VoiceGatewayClientManager(ILoggerFactory loggerFactory, DiscordShardedClient client)
    {
        _loggerFactory = loggerFactory;
        _client = client;

        _client.LeftGuild += OnLeftGuild;
        _client.VoiceServerUpdated += OnVoiceServerUpdated;
        _client.UserVoiceStateUpdated += OnUserVoiceStateUpdated;
    }

    private Task OnLeftGuild(SocketGuild guild)
    {
        return !TryGetVoiceClientForGuild(guild, out var voiceClient) ? Task.CompletedTask : DestroyClientAsync(voiceClient);
    }

    private async Task OnVoiceServerUpdated(SocketVoiceServer voiceServer)
    {
        var guild = await voiceServer.Guild.GetOrDownloadAsync();

        if (!TryGetVoiceClientForGuild(guild, out var voiceClient))
        {
            return;
        }

        await voiceClient.SetConnectionInfoAsync(null, voiceServer.Token, voiceServer.Endpoint);
    }

    private Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
    {
        if (user is not SocketGuildUser guildUser)
        {
            return Task.CompletedTask;
        }

        if (guildUser.Id != _client.CurrentUser.Id)
        {
            return Task.CompletedTask;
        }

        if (!TryGetVoiceClientForGuild(guildUser.Guild, out var voiceClient))
        {
            return Task.CompletedTask;
        }

        if (newState.VoiceChannel == null)
        {
            return DestroyClientAsync(voiceClient);
        }

        if (newState.VoiceChannel.Id == voiceClient.VoiceChannelId)
        {
            return Task.CompletedTask;
        }

        voiceClient.SetVoiceChannel(newState.VoiceChannel.Id);

        return Task.CompletedTask;
    }

    public ValueTask<VoiceGatewayClient> GetOrCreateClientAsync(
        IVoiceChannel voiceChannel,
        IAudioFrameSender audioSender,
        CancellationToken cancellationToken = default)
    {
        return TryGetVoiceClientForChannel(voiceChannel, out var voiceClient)
            ? ValueTask.FromResult(voiceClient)
            : new ValueTask<VoiceGatewayClient>(CreateClientAsync(voiceChannel, audioSender, cancellationToken));
    }

    public async Task<VoiceGatewayClient> CreateClientAsync(
        IVoiceChannel voiceChannel,
        IAudioFrameSender audioSender,
        CancellationToken cancellationToken = default)
    {
        if (_voiceClients.ContainsKey(voiceChannel.GuildId))
        {
            throw new InvalidOperationException($"A client was already created for the channel '{voiceChannel.Id}' of guild '{voiceChannel.GuildId}'.");
        }

        var voiceClient = new VoiceGatewayClient(voiceChannel.GuildId, _client.CurrentUser.Id, _loggerFactory);

        if (!_voiceClients.TryAdd(voiceChannel.GuildId, voiceClient))
        {
            return _voiceClients[voiceChannel.GuildId];
        }

        var sessionTsc = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var voiceServerTsc = new TaskCompletionSource<SocketVoiceServer>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task OnUserVoiceStateUpdatedLocal(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {
            if (user.Id == _client.CurrentUser.Id && user is IGuildUser guildUser && guildUser.Guild.Id == voiceChannel.GuildId)
            {
                if (newState.VoiceChannel?.Id == voiceChannel.Id)
                {
                    _ = sessionTsc.TrySetResult(newState.VoiceSessionId);
                }
            }

            return Task.CompletedTask;
        }

        Task OnVoiceServerUpdatedLocal(SocketVoiceServer data)
        {
            if (data.Guild.Id == voiceChannel.GuildId)
            {
                _ = voiceServerTsc.TrySetResult(data);
            }

            return Task.CompletedTask;
        }

        _client.VoiceServerUpdated += OnVoiceServerUpdatedLocal;
        _client.UserVoiceStateUpdated += OnUserVoiceStateUpdatedLocal;

        _ = await voiceChannel.ConnectAsync(false, false, true);

        await Task.WhenAll(sessionTsc.Task, voiceServerTsc.Task).WaitAsync(cancellationToken);

        _client.VoiceServerUpdated -= OnVoiceServerUpdatedLocal;
        _client.UserVoiceStateUpdated -= OnUserVoiceStateUpdatedLocal;

        var sessionId = sessionTsc.Task.Result;
        var token = voiceServerTsc.Task.Result.Token;
        var endpoint = voiceServerTsc.Task.Result.Endpoint;

        voiceClient.SetVoiceChannel(voiceChannel.Id);
        voiceClient.SetAudioFrameSender(audioSender);

        await voiceClient.SetConnectionInfoAsync(sessionId, token, endpoint, cancellationToken);

        await voiceClient.StartAsync(cancellationToken);

        return voiceClient;
    }

    public bool TryGetVoiceClientForGuild(IGuild guild, out VoiceGatewayClient voiceClient)
    {
        return TryGetVoiceClientForGuild(guild.Id, out voiceClient);
    }

    public bool TryGetVoiceClientForGuild(ulong guildId, out VoiceGatewayClient voiceClient)
    {
        return _voiceClients.TryGetValue(guildId, out voiceClient);
    }

    public bool TryGetVoiceClientForChannel(IVoiceChannel voiceChannel, out VoiceGatewayClient voiceClient)
    {
        return _voiceClients.TryGetValue(voiceChannel.GuildId, out voiceClient);
    }

    public bool TryGetVoiceClientForChannel(ulong channelId, out VoiceGatewayClient voiceClient)
    {
        var channel = _client.GetChannel(channelId);

        if (channel is not IGuildChannel guildChannel)
        {
            voiceClient = null;
            return false;
        }

        return TryGetVoiceClientForGuild(guildChannel.GuildId, out voiceClient);
    }

    public Task DestroyClientForGuildAsync(IGuild guild)
    {
        return DestroyClientForGuildAsync(guild.Id);
    }

    public Task DestroyClientForGuildAsync(ulong guildId)
    {
        return !TryGetVoiceClientForGuild(guildId, out var voiceClient) ? Task.CompletedTask : DestroyClientAsync(voiceClient);
    }

    public Task DestroyClientForChannelAsync(ulong channelId)
    {
        return !TryGetVoiceClientForChannel(channelId, out var voiceClient) ? Task.CompletedTask : DestroyClientAsync(voiceClient);
    }

    public Task DestroyClientForChannelAsync(IVoiceChannel voiceChannel)
    {
        return !TryGetVoiceClientForChannel(voiceChannel, out var voiceClient)
            ? Task.CompletedTask
            : DestroyClientAsync(voiceChannel, voiceClient);
    }

    public Task DestroyClientAsync(VoiceGatewayClient voiceClient)
    {
        var guildId = _voiceClients.FirstOrDefault(x => x.Value == voiceClient).Key;

        var guild = _client.GetGuild(guildId);

        if (guild == null)
        {
            return Task.CompletedTask;
        }

        var user = guild.GetUser(_client.CurrentUser.Id);

        return user == null ? Task.CompletedTask : DestroyClientAsync(user.VoiceChannel, voiceClient);
    }

    private async Task DestroyClientAsync(IVoiceChannel voiceChannel, VoiceGatewayClient voiceClient)
    {
        try
        {
            await voiceClient.DisposeAsync();

            if (voiceChannel != null)
            {
                await voiceChannel.DisconnectAsync();
            }
        }
        finally
        {
            if (voiceChannel != null)
            {
                _ = _voiceClients.TryRemove(voiceChannel.GuildId, out _);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client.LeftGuild -= OnLeftGuild;
        _client.VoiceServerUpdated -= OnVoiceServerUpdated;
        _client.UserVoiceStateUpdated -= OnUserVoiceStateUpdated;

        foreach (var (_, voiceClient) in _voiceClients)
        {
            try
            {
                await DestroyClientAsync(voiceClient);
            }
            catch { }
        }
    }
}
