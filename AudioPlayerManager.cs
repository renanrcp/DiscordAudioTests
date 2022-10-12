// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests;

public sealed class AudioPlayerManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<ulong, AudioPlayer> _players = new();
    private readonly DiscordShardedClient _client;
    private readonly ILoggerFactory _loggerFactory;

    public AudioPlayerManager(DiscordShardedClient client, ILoggerFactory loggerFactory)
    {
        _client = client;
        _loggerFactory = loggerFactory;

        _client.LeftGuild += OnLeftGuild;
        _client.UserVoiceStateUpdated += OnUserVoiceStateUpdated;
    }

    private Task OnLeftGuild(SocketGuild guild)
    {
        return !TryGetPlayerForGuild(guild, out var player) ? Task.CompletedTask : DestroyPlayerAsync(player);
    }

    private Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
    {
        if (user is not SocketGuildUser guildUser)
        {
            return Task.CompletedTask;
        }

        return guildUser.Id != _client.CurrentUser.Id
            ? Task.CompletedTask
            : !TryGetPlayerForGuild(guildUser.Guild, out var voiceClient)
            ? Task.CompletedTask
            : newState.VoiceChannel == null ? DestroyPlayerAsync(voiceClient) : Task.CompletedTask;
    }

    public AudioPlayer GetOrCreatePlayer(SocketTextChannel textChannel)
    {
        return TryGetPlayerForGuild(textChannel.Guild.Id, out var player) ? player : CreatePlayer(textChannel);
    }

    public AudioPlayer CreatePlayer(SocketTextChannel textChannel)
    {
        if (_players.ContainsKey(textChannel.Guild.Id))
        {
            throw new InvalidOperationException($"A player was already created for guild '{textChannel.Guild.Id}'.");
        }

        var player = new AudioPlayer(textChannel, _loggerFactory.CreateLogger<AudioPlayer>());

        return !_players.TryAdd(textChannel.Guild.Id, player) ? _players[player.Guild.Id] : player;
    }

    public bool TryGetPlayerForGuild(IGuild guild, out AudioPlayer player)
    {
        return TryGetPlayerForGuild(guild.Id, out player);
    }

    public bool TryGetPlayerForGuild(ulong guildId, out AudioPlayer player)
    {
        return _players.TryGetValue(guildId, out player);
    }

    public bool TryGetPlayerForChannel(IVoiceChannel voiceChannel, out AudioPlayer player)
    {
        return _players.TryGetValue(voiceChannel.GuildId, out player);
    }

    public bool TryGetPlayerForChannel(ulong channelId, out AudioPlayer player)
    {
        var channel = _client.GetChannel(channelId);

        if (channel is not IGuildChannel guildChannel)
        {
            player = null;
            return false;
        }

        return TryGetPlayerForGuild(guildChannel.GuildId, out player);
    }

    public Task DestroyPlayerForGuildAsync(IGuild guild)
    {
        return DestroyPlayerForGuildAsync(guild.Id);
    }

    public Task DestroyPlayerForGuildAsync(ulong guildId)
    {
        return !TryGetPlayerForGuild(guildId, out var player) ? Task.CompletedTask : DestroyPlayerAsync(player);
    }

    public Task DetroyPlayerForChannelAsync(ulong channelId)
    {
        var channel = _client.GetChannel(channelId);

        return channel is not IGuildChannel guildChannel ? Task.CompletedTask : DestroyPlayerForGuildAsync(guildChannel.GuildId);
    }

    public Task DetroyPlayerForChannelAsync(IVoiceChannel voiceChannel)
    {
        return DestroyPlayerForGuildAsync(voiceChannel.GuildId);
    }

    public async Task DestroyPlayerAsync(AudioPlayer player)
    {
        try
        {
            await player.DisposeAsync();
        }
        finally
        {
            _ = _players.TryRemove(player.Guild.Id, out _);
        }
    }


    public async ValueTask DisposeAsync()
    {
        _client.LeftGuild -= OnLeftGuild;
        _client.UserVoiceStateUpdated -= OnUserVoiceStateUpdated;

        foreach (var (_, player) in _players)
        {
            try
            {
                await DestroyPlayerAsync(player);
            }
            catch { }
        }
    }
}
