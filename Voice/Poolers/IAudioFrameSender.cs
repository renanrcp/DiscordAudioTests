// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordAudioTests.Voice.Poolers;

public interface IAudioFrameSender
{
    public ValueTask<int> ProvideFrameAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}
