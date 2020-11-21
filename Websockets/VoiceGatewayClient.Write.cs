using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordAudioTests.Websockets
{
    public partial class VoiceGatewayClient
    {
        public async Task StartChannelReaderAsync()
        {
            while (await SendReader.WaitToReadAsync(_appToken))
            {
                if (SendReader.TryRead(out var buffer))
                {
                    await _websocketClient.SendAsync(buffer, WebSocketMessageType.Text, true, _appToken);
                }
            }
        }

        private async ValueTask SendMessageAsync(ReadOnlyMemory<byte> buffer)
        {
            // Try write the buffer if has any space available in channel
            if (!SendWriter.TryWrite(buffer))
            {
                // If not try write the buffer when release a space.
                while (await SendWriter.WaitToWriteAsync(_appToken))
                {
                    // If can write the buffer release this valuetask, if not try again.
                    if (SendWriter.TryWrite(buffer))
                        return;
                }
            }
        }
    }
}