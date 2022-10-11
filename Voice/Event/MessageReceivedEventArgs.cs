// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;

namespace DiscordAudioTests.Voice.Event;

public class MessageReceivedEventArgs : EventArgs
{
    public MessageReceivedEventArgs(Memory<byte> data)
    {
        Data = data;
    }

    public Memory<byte> Data { get; }
}
