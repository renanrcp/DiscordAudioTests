// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace DiscordAudioTests
{
    public class AudioModule : ModuleBase<ShardedCommandContext>
    {
        private readonly AudioService _audioService;

        public AudioModule(AudioService audioService)
        {
            _audioService = audioService;
        }

        private AudioPlayer Player => _audioService.GetOrCreatePlayerForContext(Context);

        [Command("play")]
        public Task PlayAsync([Remainder] string url)
        {
            return _audioService.PlayAsync(Context, url);
        }

        [Command("skip")]
        public Task SkipAsync()
        {
            return Player.SkipAsync().AsTask();
        }

        [Command("pause"), Alias("resume")]
        public Task PauseOrResumeAsync()
        {
            return Player.PauseOrResumeAsync().AsTask();
        }

        [Command("seek")]
        public Task SeekAsync([Remainder] TimeSpan time)
        {
            return Player.SeekAsync(time).AsTask();
        }

        [Command("stop")]
        public Task StopAsync()
        {
            return _audioService.StopAsync(Context);
        }
    }
}
