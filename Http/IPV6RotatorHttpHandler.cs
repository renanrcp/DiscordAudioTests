// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordAudioTests.Http;

public class IPV6RotatorHttpHandler : DelegatingHandler
{
    private readonly IPV6RotatorStrategy _strategy;
    private readonly Func<HttpResponseMessage, bool> _shouldBanAddress;

    public IPV6RotatorHttpHandler(
        IPV6RotatorStrategy strategy,
        Func<HttpResponseMessage, bool> shouldBanAddres)
    {
        _strategy = strategy;
        _shouldBanAddress = shouldBanAddres;
    }

    public IPV6RotatorHttpHandler(
        IPV6RotatorStrategy strategy,
        Func<HttpResponseMessage, bool> shouldBanAddres,
        HttpMessageHandler innerHandler) : base(innerHandler)
    {
        _strategy = strategy;
        _shouldBanAddress = shouldBanAddres;
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return base.Send(request, cancellationToken);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (_shouldBanAddress(response) && request.TryGetAddressFromContext(out var address))
        {
            _strategy.AddFailedAddress(address);
        }

        return response;
    }
}
