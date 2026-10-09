using System.Text.Json.Serialization;

using EEBUS.Messages;
using Microsoft.Extensions.Logging;

namespace EEBUS.SHIP.Messages
{
    /// <summary>
    /// Part of the SHIP message exchange (SME). The SME consists of the following messages, in order:
    /// <see cref="InitMessage"/> -> 
    /// <see cref="ConnectionHelloMessage"/> -> 
    /// <see cref="ProtocolHandshakeMessage"/> -> 
    /// <see cref="PinCheckMessage"/>
    /// </summary>
    public class PinCheckMessage : ShipControlMessage<PinCheckMessage>
    {
        static PinCheckMessage()
        {
            Register(new Class());
        }

        public PinCheckMessage()
        {
        }

        public PinCheckMessage(PinStateType pinState)
        {
            this.connectionPinState.pinState = pinState;
        }

        public new class Class : ShipControlMessage<PinCheckMessage>.Class
        {
            public override PinCheckMessage Create(ReadOnlySpan<byte> data/*, Connection connection*/ )
            {
                return template.FromJsonVirtual(data/*, connection */);
            }
        }

        public ConnectionPinStateType connectionPinState { get; set; } = new();

        public override Task<(Connection.EState, Connection.ESubState, string)> ServerTestAsync(Connection.EState state, Connection? connection = null, ILogger? logger = null)
        {
            string error = string.Empty;
            Connection.EState newState = state;

            //TODO: currently we reject any pinState other than none, which is not what the spec defines.
            if (this.connectionPinState.pinState != PinStateType.none)
            {
                error = "Pinstate none expected!";
                newState = Connection.EState.Stopped;
            }
            //TODO: there is no parameter "inputPermissionSpecified", however the spec mentions a "inputPermission" parameter, which is relevant for a pinState != PinStateType.none
            //if (this.connectionPinState.inputPermissionSpecified != false)
            //{
            //    error = "Pinstate inputPermissionSpecified expected!";
            //    newState = Connection.EState.Stopped;
            //}

            return Task.FromResult((newState, Connection.ESubState.None, error));
        }

        public override async Task<(Connection.EState, Connection.ESubState)> NextServerStateAsync(Connection connection, ILogger? logger = null)
        {
            if (connection.State == Connection.EState.WaitingForPinCheck)
            {
                //We answer the peer's PinCheck by sending a PinCheck with PinStateType none, indicating we do not require a pin
                //TODO: the flow when the peer requires a pin is not yet implemented
                await new PinCheckMessage(PinStateType.none).Send(connection.WebSocket, logger).ConfigureAwait(false);
                return (Connection.EState.Connected, Connection.ESubState.None);
            }

            throw new Exception("Was waiting for PinCheck");
        }

        public override Task<(Connection.EState, Connection.ESubState, string)> ClientTestAsync(Connection.EState state, Connection? connection = null, ILogger? logger = null)
        {
            string error = string.Empty;
            Connection.EState newState = state;

            //TODO: currently we reject any pinState other than none, which is not what the spec defines.
            if (this.connectionPinState.pinState != PinStateType.none)
            {
                error = "Pinstate none expected!";
                newState = Connection.EState.Stopped;
            }

            return Task.FromResult((newState, Connection.ESubState.None, error));
        }

        public override async Task<(Connection.EState, Connection.ESubState)> NextClientStateAsync(Connection connection, ILogger? logger = null)
        {
            if (connection.State == Connection.EState.WaitingForPinCheck)
            {
                //The peer answered our PinCheck with PinStateType none, indicating it does not require a pin. At this point, the SME handshake is complete and data exchange can begin.
                //That means at this point we are ready to receive data messages. For spec compliance, we now send an AccessMethodsRequest message to the peer, which enables an easy way to reconnect
                //after the connection has been lost, without needing to rely on mdns again. However, currently this mechanism is not implemented.

                //The next step for us is to send a NodeManagementDetailedDiscovery read message, requesting the peer's node information (entities, features, functions); see Client.cs, method RequestRemoteDeviceConfiguration
                AccessMethodsRequestMessage method = new AccessMethodsRequestMessage();
                await method.Send(connection.WebSocket, logger).ConfigureAwait(false);

                return (Connection.EState.Connected, Connection.ESubState.None);
            }

            throw new Exception("Was waiting for PinCheck");
        }
    }

    [System.SerializableAttribute()]
    public class ConnectionPinStateType
    {
        public PinStateType pinState { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PinStateType
    {
        required,
        optional,
        pinOk,
        none,
    }
}
