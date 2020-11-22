using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordAudioTests.Websockets
{
    public partial class VoiceGatewayClient
    {
        private async Task StartReceiverWriterAsync()
        {
            FlushResult flushResult = default;

            try
            {
                while (!flushResult.IsCompleted && !GeneralToken.IsCancellationRequested)
                {
                    GeneralToken.ThrowIfCancellationRequested();

                    flushResult = await WriteNextMessageAsync(GeneralToken);
                }
            }
            finally
            {
                await ReceiverWriter.CompleteAsync();
            }
        }

        private async Task StartReceiverReaderAsync()
        {
            try
            {
                ReadResult readResult = default;

                while (!readResult.IsCompleted && !GeneralToken.IsCancellationRequested)
                {
                    GeneralToken.ThrowIfCancellationRequested();

                    readResult = await ReceiverReader.ReadAsync(GeneralToken);

                    var buffer = readResult.Buffer;

                    ReceiverReader.AdvanceTo(buffer.End);

                    if (TryParseJson(buffer, out var jsonPayload))
                        _ = Task.Run(() => ProcessPayloadAsync(jsonPayload));
                }
            }
            finally
            {
                await ReceiverReader.CompleteAsync();
            }
        }

        private async ValueTask<FlushResult> WriteNextMessageAsync(CancellationToken cancellationToken = default)
        {
            ValueWebSocketReceiveResult readResult = default;

            while (!readResult.EndOfMessage)
            {
                var memory = ReceiverWriter.GetMemory();

                readResult = await _websocketClient.ReceiveAsync(memory, cancellationToken);

                ReceiverWriter.Advance(readResult.Count);
            }

            return await ReceiverWriter.FlushAsync(cancellationToken);
        }

        private bool TryParseJson(ReadOnlySequence<byte> buffer, out JsonDocument jsonDocument)
        {
            try
            {
                jsonDocument = JsonDocument.Parse(buffer);
                return true;
            }
            catch
            {
                jsonDocument = null;
                return false;
            }
        }
    }
}