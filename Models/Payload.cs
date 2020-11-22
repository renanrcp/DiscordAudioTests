using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DiscordAudioTests.Models
{
    public class Payload
    {
        private static readonly IReadOnlyDictionary<PayloadOpcode, Type> _payloadTypes = new Dictionary<PayloadOpcode, Type>
        {
            { PayloadOpcode.Hello, typeof(HelloPayload) },
            { PayloadOpcode.Ready, typeof(ReadyPayload) },
        };

        public const string OPCODE_PROPERTY_NAME = "op";
        public const string PAYLOAD_PROPERTY_NAME = "d";

        public Payload(PayloadOpcode opcode, object payloadBody)
        {
            Opcode = opcode;
            PayloadBody = payloadBody;
        }

        [JsonPropertyName(OPCODE_PROPERTY_NAME)]
        public PayloadOpcode Opcode { get; set; }

        [JsonPropertyName(PAYLOAD_PROPERTY_NAME)]
        public object PayloadBody { get; set; }

        public static bool TryGetPayloadTypeByOpCode(PayloadOpcode opcode, out Type payloadType)
            => _payloadTypes.TryGetValue(opcode, out payloadType);
    }
}