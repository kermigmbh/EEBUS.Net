using System.Text.Json.Serialization;

using EEBUS.Messages;
using EEBUS.Net.EEBUS.Models;

namespace EEBUS.SHIP.Messages
{
    public class CloseMessage : ShipEndMessage<CloseMessage>
    {
        static CloseMessage()
        {
            Register(new Class());
        }

        public CloseMessage()
        {
        }

        public CloseMessage(ConnectionClosePhaseType phase)
        {
            this.connectionClose.phase = phase;
        }

        public new class Class : ShipEndMessage<CloseMessage>.Class
        {
            public override CloseMessage Create(ReadOnlySpan<byte> data)
            {
                return template.FromJsonVirtual(data);
            }
        }

        public ConnectionCloseType connectionClose { get; set; } = new();

        public override string GetId()
        {
            return "connectionClose";
        }

        public override string? GetReferencedId()
        {
            return GetId();
        }

        public override ShipMessageDirection GetMessageDirection()
        {
            return connectionClose.phase switch
            {
                ConnectionClosePhaseType.announce => ShipMessageDirection.Request,
                ConnectionClosePhaseType.confirm => ShipMessageDirection.Response,
                _ => ShipMessageDirection.Unknown
            };
        }
    }


    public class ConnectionCloseType
    {
        public ConnectionClosePhaseType phase { get; set; }

        public uint maxTime { get; set; } = 1000;

        public ConnectionCloseReasonType? reason { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ConnectionClosePhaseType
    {
        announce,
        confirm,
    }

    /// <remarks/>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ConnectionCloseReasonType
    {
        unspecific,
        removedConnection,
    }
}
