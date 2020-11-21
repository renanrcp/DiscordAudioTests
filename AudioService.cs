using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;

namespace DiscordAudioTests
{
    public class AudioService
    {
        private const string AudioFilePath = "Marshmello & Anne-Marie - FRIENDS (Lyric Video) _OFFICIAL FRIENDZONE ANTHEM.mp3";

        private readonly DiscordShardedClient _client;

        public AudioService(DiscordShardedClient client)
        {
            _client = client;
        }

        public async Task StartAsync()
        {
            var guild = _client.GetGuild(463430274823356417);
            var voiceChannel = (IVoiceChannel)_client.GetChannel(463471372589465622);
            var audioClient = await voiceChannel.ConnectAsync();


            using var ffmpeg = CreateStream(AudioFilePath);
            using var output = ffmpeg.StandardOutput.BaseStream;
            using var discordAudioStream = audioClient.CreatePCMStream(AudioApplication.Music);

            try
            {
                await output.CopyToAsync(discordAudioStream);
            }
            finally
            {
                await discordAudioStream.FlushAsync();
                discordAudioStream.Close();
                await voiceChannel.DisconnectAsync();
            }
        }

        private Process CreateStream(string path)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
        }
    }
}