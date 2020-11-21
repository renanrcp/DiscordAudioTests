using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.WebSockets;
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
                while (!flushResult.IsCompleted && !_appToken.IsCancellationRequested)
                {
                    _appToken.ThrowIfCancellationRequested();

                    flushResult = await WriteNextMessageAsync(_appToken);
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
                while (!_appToken.IsCancellationRequested)
                {
                    var payloadBytes = await ReadNextMessageAsync(_appToken);

                    await ProcessPayloadAsync(payloadBytes);
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

        private async ValueTask<ReadOnlySequence<byte>> ReadNextMessageAsync(CancellationToken cancellationToken = default)
        {
            ReadResult readResult = default;

            while (!readResult.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();

                readResult = await ReceiverReader.ReadAsync(cancellationToken);

                var buffer = readResult.Buffer;

                ReceiverReader.AdvanceTo(buffer.Start, buffer.End);
            }

            return readResult.Buffer;
        }
    }
}