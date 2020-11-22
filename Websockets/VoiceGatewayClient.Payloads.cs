using System;
using System.Threading.Tasks;
using DiscordAudioTests.Models;

namespace DiscordAudioTests.Websockets
{
    public partial class VoiceGatewayClient
    {
        private TimeSpan _heartbeatInterval;
        private int _heartbeatCount;

        private Task ProcessHelloPayloadAsync(HelloPayload payload)
        {
            _heartbeatInterval = payload.HeartbeatInterval;

            _ = _heartbeatLock.Release();

            return SendIdentifyAsync();
        }

        private Task SendIdentifyAsync()
        {
            return Task.CompletedTask;
        }

        private ValueTask SendHeartbeatAsync()
        {
            var heartbeatPayload = new Payload(PayloadOpcode.Heartbeat, _heartbeatCount);

            return SendPayloadAsync(heartbeatPayload);
        }

        private async Task StartHeartbeatAsync()
        {
            await _heartbeatLock.WaitAsync(GeneralToken);

            while (!GeneralToken.IsCancellationRequested)
            {
                GeneralToken.ThrowIfCancellationRequested();

                await Task.Delay(_heartbeatInterval, GeneralToken);

                await SendHeartbeatAsync();
            }
        }
    }
}