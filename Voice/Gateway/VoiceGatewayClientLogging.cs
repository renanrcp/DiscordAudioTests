// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System.Text.Json;
using DiscordAudioTests.Voice.Models;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests.Voice.Gateway;

public static partial class VoiceGatewayClientLogging
{
    public static void LogPayloadReceived(this ILogger logger, PayloadOpcode opcode, JsonElement payloadElement)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            var json = payloadElement.GetRawText();

            LogPayloadReceived(logger, opcode, json);
        }
    }

    public static void LogPayloadSent(this ILogger logger, Payload payload)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            var json = JsonSerializer.Serialize(payload.PayloadBody);

            LogPayloadSent(logger, payload.Opcode, json);
        }
    }

    [LoggerMessage(1, LogLevel.Trace, "Received payload opcode '{Opcode}' with body '{Json}'.", SkipEnabledCheck = true)]
    public static partial void LogPayloadReceived(this ILogger logger, PayloadOpcode opcode, string json);

    [LoggerMessage(2, LogLevel.Trace, "Sending payload opcode '{Opcode}' with body '{Json}'.", SkipEnabledCheck = true)]
    public static partial void LogPayloadSent(this ILogger logger, PayloadOpcode opcode, string json);

    [LoggerMessage(3, LogLevel.Debug, "Sending Identify payload.")]
    public static partial void LogIdentify(this ILogger logger);

    [LoggerMessage(4, LogLevel.Debug, "Sending resume payload.")]
    public static partial void LogResuming(this ILogger logger);

    [LoggerMessage(5, LogLevel.Debug, "Received hello payload with hearbeat interval '{HeartbeatInterval}'.")]
    public static partial void LogHello(this ILogger logger, float heartbeatInterval);

    [LoggerMessage(6, LogLevel.Debug, "Resumed sucessfully.")]
    public static partial void LogResumed(this ILogger logger);

    [LoggerMessage(7, LogLevel.Debug, "Successfully discovery, our address is '{Address}:{Port}'.")]
    public static partial void LogLocalAddress(this ILogger logger, string address, ushort port);

    [LoggerMessage(8, LogLevel.Debug, "Selected encryption mode: '{Mode}'.")]
    public static partial void LogEncryptionModeSelected(this ILogger logger, string mode);

    [LoggerMessage(9, LogLevel.Debug, "Generated new connection id: '{ConnectionId}'.")]
    public static partial void LogNewConnectionId(this ILogger logger, string connectionId);

    [LoggerMessage(10, LogLevel.Debug, "Waiting for session description payload.")]
    public static partial void LogWaitingSessionDescription(this ILogger logger);
}
