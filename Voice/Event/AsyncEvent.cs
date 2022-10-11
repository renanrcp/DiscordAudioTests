// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordAudioTests.Voice.Event;

public delegate Task AsyncEventHandler<TEventArgs>(TEventArgs e)
    where TEventArgs : EventArgs;

public static class AsyncEventHandlerExtensions
{
    public static IEnumerable<AsyncEventHandler<TEventArgs>> GetHandlers<TEventArgs>(this AsyncEventHandler<TEventArgs> handler)
        where TEventArgs : EventArgs
    {
        return handler.GetInvocationList().Cast<AsyncEventHandler<TEventArgs>>();
    }

    public static Task InvokeAllAsync<TEventArgs>(this AsyncEventHandler<TEventArgs> handler, TEventArgs e, CancellationToken cancellationToken = default)
        where TEventArgs : EventArgs
    {
        return Task.WhenAll(handler.GetHandlers().Select(handleAsync => handleAsync(e))).WaitAsync(cancellationToken);
    }
}
