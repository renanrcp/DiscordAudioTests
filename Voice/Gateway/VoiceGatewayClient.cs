// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DiscordAudioTests.Voice.Encrypt;
using DiscordAudioTests.Voice.Event;
using DiscordAudioTests.Voice.Models;
using DiscordAudioTests.Voice.Poolers;
using DiscordAudioTests.Voice.Websocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordAudioTests.Voice.Gateway;

public sealed class VoiceGatewayClient : IDisposable, IAsyncDisposable
{
    private readonly UdpClient _udpClient = new();
    private readonly WebSocketClient _client;
    private readonly ILogger<VoiceGatewayClient> _logger;
    private readonly SemaphoreSlim _heartbeatLock = new(0);
    private readonly Channel<ReadOnlyMemory<byte>> _sendingChannel;

    private CancellationTokenSource _cts = new();
    private string _sessionId;
    private string _token;
    private string _endpoint;
    private TimeSpan _heartbeatInterval;
    private long _lastHeartbeatSent;
    private bool _shouldResume;
    private IPEndPoint _udpEndpoint;
    private readonly OpusAudioFramePoller _framePoller;

    public VoiceGatewayClient(ulong guildId, ulong userId, string sessionId, string token, string endpoint, ILoggerFactory loggerFactory = null)
        : this(guildId, userId, loggerFactory)
    {
        _sessionId = sessionId;
        _token = token;
        _endpoint = endpoint;
    }

    public VoiceGatewayClient(ulong guildId, ulong userId, ILoggerFactory loggerFactory = null)
    {
        GuildId = guildId;
        UserId = userId;

        _sendingChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(10)
        {
            AllowSynchronousContinuations = true,
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        _framePoller = new OpusAudioFramePoller(this, loggerFactory.CreateLogger<OpusAudioFramePoller>());

        loggerFactory ??= NullLoggerFactory.Instance;
        var uri = new Uri($"wss://{_endpoint.Replace(":80", string.Empty)}?v=4");

        _client = new(uri, loggerFactory.CreateLogger<WebSocketClient>());
        _logger = loggerFactory.CreateLogger<VoiceGatewayClient>();

        _client.MessageReceived += MessageReceived;
        _client.ConnectionClosed += ConnectionClosed;
    }

    public ulong GuildId { get; }

    public ulong UserId { get; }

    public ulong VoiceChannelId { get; private set; }

    public bool Started { get; private set; }

    public long Ping { get; private set; }

    public EncryptionMode EncryptionMode { get; private set; }

    public ReadOnlyMemory<byte> SecretKey { get; private set; }

    public uint Ssrc { get; private set; }

    public IAudioFrameSender AudioFrameSender { get; private set; }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (Started)
        {
            return ValueTask.CompletedTask;
        }

        if (AudioFrameSender == null)
        {
            throw new InvalidOperationException($"Cannot start voice gateway without set an '{nameof(AudioFrameSender)}'.");
        }

        Started = true;

        _ = Task.Run(StartHeartbeatAsync, _cts.Token);
        return _client.StartAsync(cancellationToken);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (Started)
        {
            return;
        }

        if (AudioFrameSender == null)
        {
            throw new InvalidOperationException($"Cannot start voice gateway without set an '{nameof(AudioFrameSender)}'.");
        }

        Started = true;

        using var startToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        _ = Task.Run(StartHeartbeatAsync, _cts.Token);

        await _client.RunAsync(startToken.Token);
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (!Started)
        {
            return;
        }


        _client.MessageReceived -= MessageReceived;
        _client.ConnectionClosed -= ConnectionClosed;

        _cts.Cancel(false);
        _cts.Dispose();

        await _framePoller.DisposeAsync();

        try
        {
            _udpClient.Dispose();
        }
        catch { }

        await _client.StopAsync(cancellationToken);
    }

    public void SetVoiceChannel(ulong voiceChannelId)
    {
        VoiceChannelId = voiceChannelId;
    }

    public ValueTask SetConnectionInfoAsync(string sessionId, string token, string endpoint, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _sessionId = sessionId;
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            _token = token;
        }

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            _endpoint = endpoint;
        }

        if (!Started)
        {
            return ValueTask.CompletedTask;
        }

        _shouldResume = false;

        return _client.StopAsync(cancellationToken);
    }

    public async ValueTask<bool> TryReconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_cts.Token.IsCancellationRequested || !_shouldResume)
        {
            return false;
        }

        var reconnectTokenIsSameAsCts = cancellationToken == _cts.Token;

        await _framePoller.StopAsync(cancellationToken);

        _cts.Cancel(false);
        _cts = new();

        if (reconnectTokenIsSameAsCts)
        {
            cancellationToken = _cts.Token;
        }

        await _heartbeatLock.WaitAsync(cancellationToken);
        await _client.StartAsync(cancellationToken);

        return true;
    }

    public ValueTask SendSpeakingAsync(SpeakingMask speakingMask)
    {
        var speakingPayload = new Payload(PayloadOpcode.Speaking, new SpeakingPayload(Ssrc, (int)speakingMask));

        return SendPayloadAsync(speakingPayload);
    }

    public async ValueTask<int> SendFrameAsync(Memory<byte> frame, CancellationToken cancellationToken = default)
    {
        using var frameToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        return await _udpClient.SendAsync(frame, _udpEndpoint, frameToken.Token);
    }

    public void SetAudioFrameSender(IAudioFrameSender audioFrameSender)
    {
        if (AudioFrameSender == null)
        {
            return;
        }

        AudioFrameSender = audioFrameSender;
    }

    private async ValueTask SendMessageAsync(ReadOnlyMemory<byte> buffer)
    {
        // Try write the buffer if has any space available in channel
        if (!_sendingChannel.Writer.TryWrite(buffer))
        {
            // If not wait for release a space.
            while (await _sendingChannel.Writer.WaitToWriteAsync(_cts.Token))
            {
                // If can write the buffer release this valuetask, if not try again.
                if (_sendingChannel.Writer.TryWrite(buffer))
                {
                    return;
                }
            }
        }
    }

    private ValueTask SendPayloadAsync(Payload payload)
    {
        _logger.LogPayloadSent(payload);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);

        var buffer = bytes.AsMemory();

        return SendMessageAsync(buffer);
    }

    private ValueTask SendHeartbeatAsync(long heartbeat)
    {
        var heartbeatPayload = new Payload(PayloadOpcode.Heartbeat, heartbeat);

        return SendPayloadAsync(heartbeatPayload);
    }

    private ValueTask SendResumeAsync()
    {
        _shouldResume = false;

        _logger.LogResuming();

        var resumePayload = new Payload(PayloadOpcode.Resume, new ResumePayload(GuildId, _sessionId, _token));

        return SendPayloadAsync(resumePayload);
    }

    private ValueTask SendIdentifyAsync()
    {
        _logger.LogIdentify();

        var identityPayload = new Payload(PayloadOpcode.Identify, new IdentifyPayload(GuildId, UserId, _sessionId, _token));

        return SendPayloadAsync(identityPayload);
    }

    private ValueTask SendSelectProtocolAsync(string[] modes, string address, ushort port)
    {
        var mode = EncryptionModeExtensions.SelectMode(modes);

        _logger.LogEncryptionModeSelected(mode);

        var selectProtocolPayload = new SelectProtocolPayload("udp", new(address, port, mode));
        var payload = new Payload(PayloadOpcode.SelectProtocol, selectProtocolPayload);

        _logger.LogNewConnectionId(selectProtocolPayload.RtcConnectionId);

        return SendPayloadAsync(payload);
    }

    private ValueTask SendClientConnectedAsync()
    {
        var clientConnectedPayload = new Payload(PayloadOpcode.ClientConnect, new ClientConnectPayload(Ssrc));

        return SendPayloadAsync(clientConnectedPayload);
    }

    private async Task ConnectionClosed(ConnectionClosedEventArgs e)
    {
        if (await TryReconnectAsync(_cts.Token))
        {
            return;
        }

        await StopAsync();
    }

    private Task MessageReceived(MessageReceivedEventArgs e)
    {
        var jsonDocument = JsonDocument.Parse(e.Data);

        var rootElement = jsonDocument.RootElement;

        return !rootElement.TryGetProperty(Payload.OPCODE_PROPERTY_NAME, out var opcodeElement)
            ? Task.CompletedTask
            : !rootElement.TryGetProperty(Payload.PAYLOAD_PROPERTY_NAME, out var payloadElement)
            ? Task.CompletedTask
            : !opcodeElement.TryGetInt32(out var opcodeRaw)
            ? Task.CompletedTask
            : !opcodeRaw.TryParsePayloadOpcode(out var opcode) ? Task.CompletedTask : ProcessPayloadByOpcodeAsync(payloadElement, opcode).AsTask();
    }

    private ValueTask ProcessPayloadByOpcodeAsync(JsonElement payloadElement, PayloadOpcode opcode)
    {
        _logger.LogPayloadReceived(opcode, payloadElement);

        return opcode switch
        {
            PayloadOpcode.Ready => HandleReadyAsync(payloadElement.Deserialize<ReadyPayload>()),
            PayloadOpcode.Hello => HandleHelloAsync(payloadElement.Deserialize<HelloPayload>()),
            PayloadOpcode.SessionDescription => HandleSessionDescriptionAsync(payloadElement.Deserialize<SessionDescriptionPayload>()),
            PayloadOpcode.HeartbeatAck => HandleHeartbeatAckAsync(),
            PayloadOpcode.Resumed => HandleResumedAsync(),
            PayloadOpcode.Resume => ValueTask.CompletedTask,
            PayloadOpcode.Speaking => ValueTask.CompletedTask,
            PayloadOpcode.Heartbeat => ValueTask.CompletedTask,
            PayloadOpcode.SelectProtocol => ValueTask.CompletedTask,
            PayloadOpcode.Identify => ValueTask.CompletedTask,
            PayloadOpcode.Unknown => ValueTask.CompletedTask,
            PayloadOpcode.ClientDisconnect => ValueTask.CompletedTask,
            PayloadOpcode.ClientConnect => ValueTask.CompletedTask,
            _ => ValueTask.CompletedTask,
        };
    }

    private ValueTask HandleHelloAsync(HelloPayload helloPayload)
    {
        _logger.LogHello(helloPayload.HeartbeatInterval);

        _heartbeatInterval = TimeSpan.FromMilliseconds(helloPayload.HeartbeatInterval);

        _ = _heartbeatLock.Release();

        return _shouldResume ? SendResumeAsync() : SendIdentifyAsync();
    }

    private ValueTask HandleSessionDescriptionAsync(SessionDescriptionPayload sessionDescriptionPayload)
    {
        SecretKey = sessionDescriptionPayload.SecretKey;
        EncryptionMode = EncryptionModeExtensions.GetEncryptionMode(sessionDescriptionPayload.Mode);

        return _framePoller.StartAsync(_cts.Token);
    }

    private ValueTask HandleResumedAsync()
    {
        _shouldResume = true;

        _logger.LogResumed();

        return ValueTask.CompletedTask;
    }

    private ValueTask HandleHeartbeatAckAsync()
    {
        Ping = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _lastHeartbeatSent;
        return ValueTask.CompletedTask;
    }

    private async ValueTask HandleReadyAsync(ReadyPayload readyPayload)
    {
        Ssrc = readyPayload.Ssrc;
        _udpEndpoint = new(IPAddress.Parse(readyPayload.Ip), readyPayload.Port);

        var ssrcBytes = BitConverter.GetBytes(Ssrc);

        using var ipDiscoveryMemoryOwner = MemoryPool<byte>.Shared.Rent(70);

        ssrcBytes.CopyTo(ipDiscoveryMemoryOwner.Memory);

        _ = await _udpClient.SendAsync(ipDiscoveryMemoryOwner.Memory[..70], _udpEndpoint, _cts.Token);

        while (true)
        {
            var result = await _udpClient.ReceiveAsync(_cts.Token);

            _udpEndpoint = result.RemoteEndPoint;
            var buffer = result.Buffer.AsMemory();

            if (buffer.Length == 70)
            {
                var addressBuffer = buffer.Slice(4, 64).TrimEnd((byte)0);
                var portBuffer = buffer[^2..];

                var address = ParseIpAddress(addressBuffer.Span);
                var port = BitConverter.ToUInt16(portBuffer.Span);

                _logger.LogLocalAddress(address, port);

                _shouldResume = true;

                await SendSelectProtocolAsync(readyPayload.Modes, address, port);

                await SendClientConnectedAsync();

                _logger.LogWaitingSessionDescription();
            }
        }
    }

    private static string ParseIpAddress(ReadOnlySpan<byte> buffer)
    {
        var numChars = Encoding.UTF8.GetCharCount(buffer);
        Span<char> chars = stackalloc char[numChars];

        var charsWritten = Encoding.UTF8.GetChars(buffer, chars);

        return new string(chars[..charsWritten]);
    }

    private async Task StartHeartbeatAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            await _heartbeatLock.WaitAsync(_cts.Token);

            try
            {
                await Task.Delay(_heartbeatInterval, _cts.Token);

                _lastHeartbeatSent = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                await SendHeartbeatAsync(_lastHeartbeatSent);
            }
            finally
            {
                _ = _heartbeatLock.Release();
            }
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        return StopAsync();
    }
}
