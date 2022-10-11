// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;

namespace DiscordAudioTests.Voice.Models;

[Flags]
public enum SpeakingMask
{
    Normal = 1,
    SOUNDSHARE = 1 << 1,
    PRIORITY = 1 << 2,
}
