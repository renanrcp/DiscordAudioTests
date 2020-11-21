using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

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


        }

        private Task ProcessPayloadAsync(ReadOnlySequence<byte> payloadBytes)
        {
            throw new NotImplementedException();
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