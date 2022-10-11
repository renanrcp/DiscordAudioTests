// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;

namespace DiscordAudioTests.Voice.Event;

public class ClientExceptionEventArgs : EventArgs
{
    public ClientExceptionEventArgs(Exception exception)
    {
        Exception = exception;
    }

    public Exception Exception { get; }
}
