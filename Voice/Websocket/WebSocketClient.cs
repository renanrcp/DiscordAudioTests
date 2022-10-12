// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DiscordAudioTests.Voice.Event;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests.Voice.Websocket;

public sealed class WebSocketClient : IDisposable, IAsyncDisposable
{
    private readonly Uri _uri;

    private const int RECOMMENDED_BUFFER_SIZE = 1024 * 16;

    public event AsyncEventHandler<MessageReceivedEventArgs> MessageReceived;
    public event AsyncEventHandler<ConnectionClosedEventArgs> ConnectionClosed;
    public event AsyncEventHandler<ClientExceptionEventArgs> ClientException;

    private readonly ILogger<WebSocketClient> _logger;
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    private ClientWebSocket _client;

    public WebSocketClient(Uri uri, ILogger<WebSocketClient> logger)
    {
        _uri = uri;
        _logger = logger;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (_client != null)
        {
            return ValueTask.CompletedTask;
        }

        _ = Task.Run(() => RunAsync(cancellationToken), cancellationToken);

        return ValueTask.CompletedTask;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (_client != null)
        {
            return;
        }

        _client = new();

        try
        {
            _logger.LogConnecting(_uri);

            await _client.ConnectAsync(_uri, cancellationToken);

            using var memoryOwner = MemoryPool<byte>.Shared.Rent(RECOMMENDED_BUFFER_SIZE);
            var bytesReaded = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await _client.ReceiveAsync(memoryOwner.Memory[bytesReaded..], cancellationToken);

                if (!result.EndOfMessage)
                {
                    continue;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (MessageReceived != null)
                {
                    var messageReceivedEvent = new MessageReceivedEventArgs(memoryOwner.Memory[bytesReaded..]);
                    await MessageReceived.InvokeAllAsync(messageReceivedEvent, cancellationToken);
                }
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogWebSocketDisconnect();
            _logger.LogWebSocketException(ex);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWebSocketError(ex, ex.Message);

            if (ClientException != null)
            {
                var clientExceptionEvent = new ClientExceptionEventArgs(ex);

                var clientExceptionCancelToken = cancellationToken.IsCancellationRequested
                    ? CancellationToken.None
                    : cancellationToken;

                await ClientException.InvokeAllAsync(clientExceptionEvent, clientExceptionCancelToken);
            }
        }
        finally
        {
            try
            {
                _client.Dispose();
            }
            catch { }

            _client = null;

            if (ConnectionClosed != null)
            {
                var closedEvent = new ConnectionClosedEventArgs(_client.CloseStatus, _client.CloseStatusDescription);

                var closedCancelToken = cancellationToken.IsCancellationRequested
                    ? CancellationToken.None
                    : cancellationToken;

                await ConnectionClosed.InvokeAllAsync(closedEvent, closedCancelToken);
            }
        }
    }

    public async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (_client == null || _client.State != WebSocketState.Open)
        {
            return;
        }

        await _sendSemaphore.WaitAsync(cancellationToken);

        try
        {
            var bytesWritten = 0;
            const int recomendedSendBufferSize = 1024 * 4;

            while (bytesWritten < buffer.Length)
            {
                var endBufferPos = Math.Min(bytesWritten + recomendedSendBufferSize, buffer.Length);
                var sendBuffer = buffer.Slice(bytesWritten, endBufferPos);
                var endOfMessage = bytesWritten + endBufferPos >= buffer.Length;

                await _client.SendAsync(sendBuffer, WebSocketMessageType.Text, endOfMessage, cancellationToken);
            }
        }
        finally
        {
            _ = _sendSemaphore.Release();
        }
    }


    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_client == null || _client.State != WebSocketState.Open)
        {
            return;
        }

        try
        {
            await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected by client.", cancellationToken);
        }
        catch { }
        finally
        {
            try
            {
                _client.Dispose();
            }
            catch { }

            _client = null;
        }
    }


    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await StopAsync();
        }
    }
}
