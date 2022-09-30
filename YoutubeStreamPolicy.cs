// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace DiscordAudioTests;

public class YoutubeStreamPolicy : AsyncPolicy<HttpResponseMessage>, IRetryPolicy, IsPolicy
{
    private const int PermittedRetryCount = 2;

    private YoutubeStreamPolicy(PolicyBuilder<HttpResponseMessage> policyBuilder) : base(policyBuilder)
    {
    }

    protected override async Task<HttpResponseMessage> ImplementationAsync(Func<Context, CancellationToken, Task<HttpResponseMessage>> action, Context context, CancellationToken cancellationToken, bool continueOnCapturedContext)
    {
        var tryCount = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool canRetry;

            try
            {
                var result = await action(context, cancellationToken).ConfigureAwait(continueOnCapturedContext);

                if (!ResultPredicates.AnyMatch(result))
                {
                    return result;
                }

                canRetry = tryCount < PermittedRetryCount;

                if (!canRetry)
                {
                    return result;
                }

                if (((int)result.StatusCode) >= 500 && tryCount > 1)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                var handledException = ExceptionPredicates.FirstMatchOrDefault(ex);
                if (handledException == null)
                {
                    throw;
                }

                canRetry = tryCount < PermittedRetryCount;

                if (!canRetry)
                {
                    throw;
                }
            }

            if (tryCount < int.MaxValue) { tryCount++; }
        }
    }

    public static YoutubeStreamPolicy CreatePolicy()
    {
        var builder = Policy<HttpResponseMessage>
                        .Handle<HttpRequestException>()
                        .OrResult((response) =>
                        {
                            return (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout;
                        });

        return new(builder);
    }
}
