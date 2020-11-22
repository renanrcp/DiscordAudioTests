using System;
using System.Threading.Tasks;
using DiscordAudioTests.Models;

namespace DiscordAudioTests.Websockets
{
    public partial class VoiceGatewayClient
    {
        private TimeSpan _heartbeatInterval;
        private int _heartbeatCount;

        private ValueTask ProcessReadyPayloadAsync(ReadyPayload payload)
        {
            return default;
        }

        private ValueTask ProcessHelloPayloadAsync(HelloPayload payload)
        {
            _heartbeatInterval = TimeSpan.FromMilliseconds(payload.HeartbeatInterval);

            _ = _heartbeatLock.Release();

            return SendIdentifyAsync();
        }

        private ValueTask SendIdentifyAsync()
        {
            var identityPayload = new Payload(PayloadOpcode.Identify, new IdentifyPayload(_connectionInfo));

            return SendPayloadAsync(identityPayload);
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

                _heartbeatCount++;

                await SendHeartbeatAsync();
            }
        }
    }
}