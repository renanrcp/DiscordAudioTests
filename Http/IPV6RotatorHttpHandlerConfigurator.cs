// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DiscordAudioTests.Http;

public static class IPV6RotatorHttpHandlerConfigurator
{
    public const int DEFAULTRETRYLIMIT = 4;

    public static void ConfigureIPV6RotatorStrategy(
        this SocketsHttpHandler handler,
        IPV6RotatorStrategy strategy,
        ILogger logger)
    {
        handler.ConnectCallback = (context, cancellationToken) =>
        {
            return ConnectAsync(context, strategy, logger, cancellationToken);
        };
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        IPV6RotatorStrategy strategy,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var tryCount = 0;

        while (true)
        {
            var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, AddressFamily.InterNetworkV6, cancellationToken);

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

            IPAddress iPAddress = null;

            try
            {
                iPAddress = strategy.GetIPAddress();

                socket.Bind(new IPEndPoint(iPAddress, 0));

                logger.LogDebug("Socket binded with ip {ipAddress}", iPAddress);

                context.InitialRequestMessage.SetAddressInContext(iPAddress);

                await socket.ConnectAsync(entry.AddressList, context.DnsEndPoint.Port, cancellationToken);

                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex)
            {
                socket.Dispose();

                var canRetry = tryCount < DEFAULTRETRYLIMIT;

                if (!canRetry)
                {
                    throw;
                }

                if (ex is not SocketException socketEx)
                {
                    socketEx = ex.InnerException as SocketException;
                }

                if (socketEx == null)
                {
                    throw;
                }

                if (socketEx.SocketErrorCode != SocketError.AddressNotAvailable)
                {
                    throw;
                }

                if (iPAddress != null)
                {
                    logger.LogError("Cannot bind socket for ip {ipAddress}", iPAddress);
                }

                strategy.AddFailedAddress(iPAddress);
            }
        }
    }
}
