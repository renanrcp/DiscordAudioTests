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
    private const int RECOMMENDED_BUFFER_SIZE = 1024 * 16;

    public event AsyncEventHandler<MessageReceivedEventArgs> MessageReceived;
    public event AsyncEventHandler<ConnectionClosedEventArgs> ConnectionClosed;
    public event AsyncEventHandler<ClientExceptionEventArgs> ClientException;

    private readonly ILogger<WebSocketClient> _logger;
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    private ClientWebSocket _client;

    public WebSocketClient(ILogger<WebSocketClient> logger)
    {
        _logger = logger;
    }

    public ValueTask StartAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        if (_client != null)
        {
            return ValueTask.CompletedTask;
        }

        _ = Task.Run(() => RunAsync(uri, cancellationToken), cancellationToken);

        return ValueTask.CompletedTask;
    }

    public async Task RunAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        if (_client != null)
        {
            return;
        }

        _client = new();

        try
        {
            _logger.LogConnecting(uri);

            await _client.ConnectAsync(uri, cancellationToken);

            using var memoryOwner = MemoryPool<byte>.Shared.Rent(RECOMMENDED_BUFFER_SIZE);
            var bytesReaded = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await _client.ReceiveAsync(memoryOwner.Memory[bytesReaded..], cancellationToken);

                bytesReaded += result.Count;

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
                    var messageReceivedEvent = new MessageReceivedEventArgs(memoryOwner.Memory[..bytesReaded]);

                    bytesReaded = 0;

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
            if (ConnectionClosed == null)
            {
                DisposeClient();
            }
            else
            {
                var closedEvent = new ConnectionClosedEventArgs(_client.CloseStatus, _client.CloseStatusDescription);

                var closedCancelToken = cancellationToken.IsCancellationRequested
                    ? CancellationToken.None
                    : cancellationToken;

                DisposeClient();

                await ConnectionClosed.InvokeAllAsync(closedEvent, closedCancelToken);
            }
        }
    }

    private void DisposeClient()
    {
        if (_client == null)
        {
            return;
        }

        try
        {
            _client.Dispose();
        }
        catch { }

        _client = null;
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

                bytesWritten += sendBuffer.Length;

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
            DisposeClient();
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
