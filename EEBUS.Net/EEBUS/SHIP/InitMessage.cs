using EEBUS.Enums;
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
    public class InitMessage : ShipInitMessage<InitMessage>
    {
        static InitMessage()
        {
            Register(new Class());
        }

        public InitMessage()
        {
        }

        public new class Class : ShipInitMessage<InitMessage>.Class
        {
            public override InitMessage Create(ReadOnlySpan<byte> data/*, Connection connection*/ )
            {
                return template.FromJsonVirtual(data/*, connection */);
            }
        }

        public override byte[] bytes { get; set; } = { SHIPMessageType.INIT, SHIPMessageValue.CMI_HEAD };


        public override async Task<(Connection.EState, Connection.ESubState, string)> ServerTestAsync(Connection.EState state, Connection? connection = null, ILogger? logger = null)
        {
            string error = string.Empty;
            Connection.EState newState = state;

            if (this.bytes[1] != SHIPMessageValue.CMI_HEAD)
            {
                error = "Expected SMI_HEAD payload in INIT message!";
                newState = Connection.EState.Stopped;
                if (connection != null)
                {
                    await new InitMessage().Send(connection.WebSocket, logger).ConfigureAwait(false);   //Send back a normal init with CmiHead = 0 and close the connection
                }
            }

            return (newState, Connection.ESubState.None, error);
        }

        public override async Task<(Connection.EState, Connection.ESubState)> NextServerStateAsync(Connection connection, ILogger? logger = null)
        {
            if (connection.State == Connection.EState.Disconnected || connection.State == Connection.EState.Connected)
            {
                //Send an init answer back to the peer
                await new InitMessage().Send(connection.WebSocket, logger).ConfigureAwait(false);

                //We received an init message, so we can enter the next SME state and send a hello message. We also start the WaitForReadyTimer; if we do not receive a hello answer in time, we will abort the connection.
                connection.StartWaitForReadyTimer();
                await new ConnectionHelloMessage(ConnectionHelloPhaseType.ready, connection.WaitForReadyTimerValue).Send(connection.WebSocket, logger).ConfigureAwait(false);
                return (Connection.EState.WaitingForConnectionHello, Connection.ESubState.None);
            }

            throw new Exception("Was waiting for Init");
        }

        public override Task<(Connection.EState, Connection.ESubState, string)> ClientTestAsync(Connection.EState state, Connection? connection = null, ILogger? logger = null)
        {
            string error = string.Empty;
            Connection.EState newState = state;

            if (this.bytes[1] != SHIPMessageValue.CMI_HEAD)
            {
                error = "Expected SMI_HEAD payload in INIT message!";
                newState = Connection.EState.Stopped;
            }

            return Task.FromResult((newState, Connection.ESubState.None, error));
        }

        public override async Task<(Connection.EState, Connection.ESubState)> NextClientStateAsync(Connection connection, ILogger? logger = null)
        {
            if (connection.State == Connection.EState.Disconnected || connection.State == Connection.EState.Connected)
            {
                //As a client, we already sent an init message at the start, so after receiving the peer's init messsage, we can enter the next SME state and send a hello message. We also start the WaitForReadyTimer; if we do not receive a hello answer in time, we will abort the connection.
                connection.StartWaitForReadyTimer();
                ConnectionHelloMessage message = new ConnectionHelloMessage(ConnectionHelloPhaseType.ready, connection.WaitForReadyTimerValue);
                await message.Send(connection.WebSocket, logger).ConfigureAwait(false);

                return (Connection.EState.WaitingForConnectionHello, Connection.ESubState.None);
            }

            throw new Exception("Was waiting for Init");
        }
    }
}
