using System;
using System.Buffers;
using System.Collections.Generic;
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

                    if (TryParseJsons(buffer, out var jsonPayloads))
                    {
                        foreach (var jsonPayload in jsonPayloads)
                            await ProcessPayloadAsync(jsonPayload);
                    }

                    ReceiverReader.AdvanceTo(buffer.End);
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

        private bool TryParseJsons(ReadOnlySequence<byte> buffer, out IEnumerable<JsonDocument> jsonDocuments)
        {
            var jsons = new List<JsonDocument>();
            SequencePosition? firstPosition = null;
            SequencePosition? lastPosition = null;

            do
            {
                if (buffer.Length <= 0)
                    break;

                firstPosition = GetJsonStartPosition(buffer);
                lastPosition = GetJsonEndPosition(buffer);

                if (firstPosition != null && lastPosition != null)
                {
                    var jsonBuffer = buffer.Slice(firstPosition.Value, lastPosition.Value);

                    if (!TryParseJson(jsonBuffer, out var jsonDocument))
                    {
                        jsonDocuments = jsons;
                        return false;
                    }

                    jsons.Add(jsonDocument);

                    buffer = buffer.Slice(lastPosition.Value);
                }
            }
            while (lastPosition != null);

            jsonDocuments = jsons;
            return jsons.Count > 0;
        }

        private SequencePosition? GetJsonStartPosition(ReadOnlySequence<byte> buffer)
            => buffer.PositionOf((byte)'{');

        private SequencePosition? GetJsonEndPosition(ReadOnlySequence<byte> buffer)
        {
            var sum = 0;
            var startPosition = buffer.Start;

            if (!buffer.TryGet(ref startPosition, out var memoryBuffer))
                return null;

            while (true)
            {
                var jsonFinalPosition = memoryBuffer.Span.IndexOf((byte)'}') + 1;

                if (memoryBuffer.Span.Length <= jsonFinalPosition)
                    return buffer.GetPosition(jsonFinalPosition + sum);

                var nextValue = memoryBuffer.Span[jsonFinalPosition];

                if (nextValue == (byte)'{')
                    return buffer.GetPosition(jsonFinalPosition + sum);

                memoryBuffer = memoryBuffer.Slice(jsonFinalPosition);

                sum += jsonFinalPosition;
            }
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