// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests.Voice.Websocket;

public static partial class WebSocketClientLogging
{
    [LoggerMessage(1, LogLevel.Warning, "Websocket disconnected, check your internet connection.")]
    public static partial void LogWebSocketDisconnect(this ILogger logger);

    [LoggerMessage(2, LogLevel.Debug, "Exception throwed in WebSocketClient.")]
    public static partial void LogWebSocketException(this ILogger logger, Exception exception);

    [LoggerMessage(3, LogLevel.Error, "Error in WebSocketClient. {Message}")]
    public static partial void LogWebSocketError(this ILogger logger, Exception exception, string message);
}
