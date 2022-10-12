// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;

namespace DiscordAudioTests.Voice.Models;

[Flags]
public enum SpeakingMask
{
    None = 0,
    Microphone = 1 << 0,
    Soundshare = 1 << 1,
    Priority = 1 << 2,
}
