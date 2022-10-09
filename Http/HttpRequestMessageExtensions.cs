// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;

namespace DiscordAudioTests.Http;

public static class HttpRequestMessageExtensions
{
    private const string AddressContextRequestKey = "IPV6RotatorStrategy::LocalUsedAddres";

    public static void SetAddressInContext(this HttpRequestMessage requestMessage, IPAddress address)
    {
        requestMessage.Options.Set(new HttpRequestOptionsKey<IPAddress>(AddressContextRequestKey), address);
    }

    public static bool TryGetAddressFromContext(this HttpRequestMessage requestMessage, out IPAddress address)
    {
        return requestMessage.Options.TryGetValue(new HttpRequestOptionsKey<IPAddress>(AddressContextRequestKey), out address);
    }
}
