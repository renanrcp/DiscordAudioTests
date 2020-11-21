using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DiscordAudioTests.Models;

namespace DiscordAudioTests.Websockets
{
    public partial class VoiceGatewayClient : IAsyncDisposable
    {
        private readonly CancellationToken _appToken;
        private readonly ConnectionInfo _connectionInfo;
        private readonly ClientWebSocket _websocketClient;
        private readonly Channel<ReadOnlyMemory<byte>> _sendingChannel;
        private readonly Pipe _pipeReceiver;

        public VoiceGatewayClient(ConnectionInfo connectionInfo, CancellationToken cancellationToken)
        {
            _connectionInfo = connectionInfo;
            _appToken = cancellationToken;
            _websocketClient = new ClientWebSocket();
            _sendingChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(
            new BoundedChannelOptions(10)
            {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });
            _pipeReceiver = new Pipe();
        }

        private ChannelWriter<ReadOnlyMemory<byte>> SendWriter => _sendingChannel.Writer;

        private ChannelReader<ReadOnlyMemory<byte>> SendReader => _sendingChannel.Reader;

        private PipeWriter ReceiverWriter => _pipeReceiver.Writer;

        private PipeReader ReceiverReader => _pipeReceiver.Reader;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var cts = GetCancellationToken(cancellationToken);

            var webSocketUri = new Uri($"wss://{_connectionInfo.Endpoint}?v=4");

            await _websocketClient.ConnectAsync(webSocketUri, cts);

            _ = Task.Run(StartReceiverWriterAsync);
            _ = Task.Run(StartReceiverReaderAsync);
            _ = Task.Run(StartChannelReaderAsync);
        }

        private Task ProcessPayloadAsync(ReadOnlySequence<byte> payloadBytes)
        {
            var jsonDocument = JsonDocument.Parse(payloadBytes);

            var rootElement = jsonDocument.RootElement;

            if (!rootElement.TryGetProperty(Payload.OPCODE_PROPERTY_NAME, out var opcodeElement))
                return Task.CompletedTask;

            if (!rootElement.TryGetProperty(Payload.PAYLOAD_PROPERTY_NAME, out var payloadElement))
                return Task.CompletedTask;

            if (!opcodeElement.TryGetInt32(out var opcodeRaw))
                return Task.CompletedTask;

            if (!TryParsePayloadOpcode(opcodeRaw, out var opcode))
                return Task.CompletedTask;

            if (!Payload.TryGetPayloadTypeByOpCode(opcode, out var payloadType))
                return Task.CompletedTask;

            return ProcessPayloadByTypeAsync(payloadType, payloadElement);
        }

        private Task ProcessPayloadByTypeAsync(Type payloadType, JsonElement payloadElement)
        {
            var payload = JsonSerializer.Deserialize(payloadElement.GetRawText(), payloadType);

            return payload switch
            {
                _ => Task.CompletedTask,
            };
        }

        private bool TryParsePayloadOpcode(int opcodeRaw, out PayloadOpcode opcode)
        {
            try
            {
                opcode = (PayloadOpcode)opcodeRaw;
                return true;
            }
            catch
            {
                opcode = PayloadOpcode.Unknown;
                return false;
            }
        }

        public ValueTask DisposeAsync()
        {
            _websocketClient.Dispose();

            return default;
        }

        private CancellationToken GetCancellationToken(CancellationToken cancellationToken)
            => CancellationTokenSource.CreateLinkedTokenSource(_appToken, cancellationToken).Token;
    }
}