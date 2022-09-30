// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DiscordAudioTests;

public class AudioModule : ModuleBase<ShardedCommandContext>
{
    private readonly AudioService _audioService;

    public AudioModule(AudioService audioService)
    {
        _audioService = audioService;
    }

    private AudioPlayer Player => _audioService.GetOrCreatePlayerForContext(Context);

    private bool UserConnected => (Context.User as IGuildUser).VoiceChannel != null;

    [Command("play")]
    public async Task PlayAsync([Remainder] string url)
    {
        if (!UserConnected)
        {
            _ = await ReplyAsync("You're not connected in a voice channel.");
            return;
        }

        var directPlayed = Player.Queue.IsEmpty;

        await _audioService.PlayAsync(Context, url);

        if (directPlayed)
        {
            return;
        }

        _ = await ReplyAsync($"Song '{url}' added to queue.");
    }

    [Command("skip")]
    public async Task SkipAsync()
    {
        if (!UserConnected)
        {
            _ = await ReplyAsync("You're not connected in a voice channel.");
            return;
        }

        if (Player.Queue.IsEmpty)
        {
            _ = await ReplyAsync("No songs to Skip.");
            return;
        }

        await Player.SkipAsync();
        _ = await ReplyAsync("song skipped.");
    }

    [Command("pause"), Alias("resume")]
    public async Task PauseOrResumeAsync()
    {
        if (!UserConnected)
        {
            _ = await ReplyAsync("You're not connected in a voice channel.");
            return;
        }

        var paused = Player.IsPaused;

        await Player.PauseOrResumeAsync();

        if (paused)
        {
            _ = await ReplyAsync("Player resumed.");
            return;
        }

        _ = await ReplyAsync("Player paused.");
    }

    // disabled for now
    // [Command("seek")]
    // public Task SeekAsync([Remainder] TimeSpan time)
    // {
    //     return Player.SeekAsync(time).AsTask();
    // }

    [Command("stop")]
    public async Task StopAsync()
    {
        if (!UserConnected)
        {
            _ = await ReplyAsync("You're not connected in a voice channel.");
            return;
        }

        await _audioService.StopAsync(Context);
        _ = await ReplyAsync("Player stopped.");
    }
}
