// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Net.WebSockets;

namespace DiscordAudioTests.Voice.Event;

public class ConnectionClosedEventArgs : EventArgs
{
    public ConnectionClosedEventArgs(WebSocketCloseStatus? closeStatus, string closeStatusDescription)
    {
        CloseStatus = closeStatus;
        CloseStatusDescription = closeStatusDescription;
        CloseMessage = GetCloseMessage(CloseStatus);
    }

    public WebSocketCloseStatus? CloseStatus { get; }

    public string CloseStatusDescription { get; }

    public string CloseMessage { get; }

    private static string GetCloseMessage(WebSocketCloseStatus? status)
    {
        var code = status != null
                    ? (int)status
                    : 0;

        return code switch
        {
            4001 => "You sent an invalid opcode.",
            4002 => "You sent an invalid payload in your identifying to the Gateway.",
            4003 => "You sent a payload before identifying with the Gateway.",
            4004 => "The token you sent in your identify payload is incorrect.",
            4005 => "You sent more than one identify payload",
            4006 => "Your session is no longer valid.",
            4009 => "Your session has timed out.",
            4011 => "We can't find the server you're trying to connect to.",
            4012 => "We didn't recognize the protocol you sent.",
            4014 => "Channel was deleted, you were kicked, voice server changed, or the main gateway session was dropped. Should not reconnect.",
            4015 => "The server crashed. Our bad! Try resuming.",
            4016 => "We didn't recognize your encryption.",
            _ => "Unknown error.",
        };
    }
}
