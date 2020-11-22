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
        private readonly CancellationTokenSource _generalCts;
        private readonly ConnectionInfo _connectionInfo;
        private readonly ClientWebSocket _websocketClient;
        private readonly Channel<ReadOnlyMemory<byte>> _sendingChannel;
        private readonly Pipe _pipeReceiver;
        private readonly SemaphoreSlim _heartbeatLock;

        public VoiceGatewayClient(ConnectionInfo connectionInfo, CancellationToken cancellationToken)
        {
            _connectionInfo = connectionInfo;
            _generalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
            _heartbeatLock = new SemaphoreSlim(0);
        }

        private CancellationToken GeneralToken => _generalCts.Token;

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
            _ = Task.Run(StartHeartbeatAsync);
        }

        private ValueTask ProcessPayloadAsync(ReadOnlySequence<byte> payloadBytes)
        {
            var jsonDocument = JsonDocument.Parse(payloadBytes);

            var rootElement = jsonDocument.RootElement;

            if (!rootElement.TryGetProperty(Payload.OPCODE_PROPERTY_NAME, out var opcodeElement))
                return default;

            if (!rootElement.TryGetProperty(Payload.PAYLOAD_PROPERTY_NAME, out var payloadElement))
                return default;

            if (!opcodeElement.TryGetInt32(out var opcodeRaw))
                return default;

            if (!TryParsePayloadOpcode(opcodeRaw, out var opcode))
                return default;

            if (!Payload.TryGetPayloadTypeByOpCode(opcode, out var payloadType))
                return default;

            return ProcessPayloadByTypeAsync(payloadType, payloadElement);
        }

        private ValueTask ProcessPayloadByTypeAsync(Type payloadType, JsonElement payloadElement)
        {
            var payload = JsonSerializer.Deserialize(payloadElement.GetRawText(), payloadType);

            return payload switch
            {
                HelloPayload => ProcessHelloPayloadAsync((HelloPayload)payload),
                _ => default,
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
            => CancellationTokenSource.CreateLinkedTokenSource(GeneralToken, cancellationToken).Token;
    }
}