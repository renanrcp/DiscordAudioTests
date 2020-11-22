using System;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordAudioTests.Websockets
{
    public partial class VoiceGatewayClient
    {
        private async Task StartChannelReaderAsync()
        {
            while (await SendReader.WaitToReadAsync(GeneralToken))
            {
                if (SendReader.TryRead(out var buffer))
                {
                    await _websocketClient.SendAsync(buffer, WebSocketMessageType.Text, true, GeneralToken);
                }
            }
        }

        private async ValueTask SendMessageAsync(ReadOnlyMemory<byte> buffer)
        {
            // Try write the buffer if has any space available in channel
            if (!SendWriter.TryWrite(buffer))
            {
                // If not wait for release a space.
                while (await SendWriter.WaitToWriteAsync(GeneralToken))
                {
                    // If can write the buffer release this valuetask, if not try again.
                    if (SendWriter.TryWrite(buffer))
                        return;
                }
            }
        }

        private ValueTask SendPayloadAsync(object payload)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);

            var buffer = bytes.AsMemory();

            return SendMessageAsync(buffer);
        }
    }
}