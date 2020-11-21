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
        private readonly Channel<ArrayBufferWriter<byte>> _sendingChannel;
        private readonly Pipe _pipeReceiver;

        public VoiceGatewayClient(ConnectionInfo connectionInfo, CancellationToken cancellationToken)
        {
            _connectionInfo = connectionInfo;
            _appToken = cancellationToken;
            _websocketClient = new ClientWebSocket();
            _sendingChannel = Channel.CreateBounded<ArrayBufferWriter<byte>>(
            new BoundedChannelOptions(10)
            {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });
            _pipeReceiver = new Pipe();
        }

        private ChannelWriter<ArrayBufferWriter<byte>> SendWriter => _sendingChannel.Writer;

        private ChannelReader<ArrayBufferWriter<byte>> SendReader => _sendingChannel.Reader;

        private PipeWriter ReceiverWriter => _pipeReceiver.Writer;

        private PipeReader ReceiverReader => _pipeReceiver.Reader;

        public ValueTask DisposeAsync()
        {
            _websocketClient.Dispose();

            return default;
        }
    }
}