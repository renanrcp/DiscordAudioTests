// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System.Net.Http;

namespace DiscordAudioTests.Http;

public static class HttpClientFactoryExtensions
{
    public static HttpClient CreateClient<TClient>(this IHttpClientFactory httpClientFactory)
        where TClient : class
    {
        return httpClientFactory.CreateClient(typeof(TClient).Name);
    }
}
