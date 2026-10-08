using EEBUS.Enums;
using EEBUS.Messages;
using EEBUS.Net;
using EEBUS.UseCases.ControllableSystem;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace EEBUS.SHIP.Messages
{
    /// <summary>
    /// Part of the SHIP message exchange (SME). The SME consists of the following messages, in order:
    /// <see cref="InitMessage"/> -> 
    /// <see cref="ConnectionHelloMessage"/> -> 
    /// <see cref="ProtocolHandshakeMessage"/> -> 
    /// <see cref="PinCheckMessage"/>
    /// </summary>
    public class ConnectionHelloMessage : ShipControlMessage<ConnectionHelloMessage>
    {
        static ConnectionHelloMessage()
        {
            Register(new Class());
        }

        public ConnectionHelloMessage()
        {
        }

        public ConnectionHelloMessage(ConnectionHelloPhaseType phase)
        {
            this.connectionHello.phase = phase;
        }

        public ConnectionHelloMessage(ConnectionHelloPhaseType phase, uint? waiting)
        {
            this.connectionHello.phase = phase;
            this.connectionHello.waiting = waiting;
        }

        public new class Class : ShipControlMessage<ConnectionHelloMessage>.Class
        {
            public override ConnectionHelloMessage Create(ReadOnlySpan<byte> data/*, Connection connection*/ )
            {
                return template.FromJsonVirtual(data/*, connection*/ );
            }
        }

        public ConnectionHelloType connectionHello { get; set; } = new();

        public override async Task<(Connection.EState, Connection.ESubState)> NextServerStateAsync(Connection connection, ILogger? logger = null)
        {
            //TODO: spec says that if we receive a message that is not a hello message while in this state, we should send an abort message

            if (connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.ready)
            {
                connection.StopWaitForReadyTimer();
                return (Connection.EState.WaitingForProtocolHandshake, Connection.ESubState.None);
            }

            if (connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.pending && connection.SubState == Connection.ESubState.None)
            {
                if (this.connectionHello.prolongationRequest == true)
                {
                    //grant prolongation request by increasing the Wait-For-Ready-Timer and sending a hello message with state ready and the current value of the Wait-For-Ready-Timer
                    connection.ProlongHelloDeadline();
                    await new ConnectionHelloMessage(ConnectionHelloPhaseType.ready, connection.WaitForReadyTimerValue).Send(connection.WebSocket, logger).ConfigureAwait(false);
                    return (Connection.EState.WaitingForConnectionHello, Connection.ESubState.FirstPending);
                } else
                {
                    return (connection.State, connection.SubState); //if we receive a pending hello message that is not a prolongation request, no action is required, we can ignore it
                }
            }

            if (connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.pending && connection.SubState == Connection.ESubState.FirstPending)
            {
                //We have received one pending message with a prolongation request, so we can accept a second one
                if (this.connectionHello.prolongationRequest == true)
                {
                    connection.ProlongHelloDeadline();
                    await new ConnectionHelloMessage(ConnectionHelloPhaseType.ready, connection.WaitForReadyTimerValue).Send(connection.WebSocket, logger).ConfigureAwait(false);
                    return (Connection.EState.WaitingForConnectionHello, Connection.ESubState.SecondPending);
                } else
                {
                    return (connection.State, connection.SubState); //if we receive a pending hello message that is not a prolongation request, we can ignore it
                }
            }

            if (connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.pending && connection.SubState == Connection.ESubState.SecondPending)
            {
                await UpdateConnectionStatusAsync(connection, DeviceConnectionStatus.Aborted).ConfigureAwait(false);
                await new ConnectionHelloMessage(ConnectionHelloPhaseType.aborted).Send(connection.WebSocket, logger).ConfigureAwait(false);
                return (Connection.EState.Stopped, Connection.ESubState.None);
            }

            if (connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.aborted)
            {
                await UpdateConnectionStatusAsync(connection, DeviceConnectionStatus.Aborted).ConfigureAwait(false);
                return (Connection.EState.Stopped, Connection.ESubState.None);
            }

            throw new Exception("Hello aborted!");
        }

        private async Task UpdateConnectionStatusAsync(Connection connection, DeviceConnectionStatus status)
        {
            connection.ConnectionStatus = status;
            List<DeviceConnectionStatusEvents> notifyEvents = connection.Local.GetUseCaseEvents<DeviceConnectionStatusEvents>();
            foreach (var ev in notifyEvents)
            {
                await ev.DeviceConnectionStatusUpdatedAsync(connection);
            }
        }

        public override async Task<(Connection.EState, Connection.ESubState)> NextClientStateAsync(Connection connection, ILogger? logger = null)
        {
            if (connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.ready)
            {
                connection.StopWaitForReadyTimer();
                ProtocolHandshakeMessage message = new ProtocolHandshakeMessage(ProtocolHandshakeTypeType.announceMax, SHIPVersion.MAX_MAJOR, SHIPVersion.MAX_MINOR);
                await message.Send(connection.WebSocket, logger).ConfigureAwait(false);
                return (Connection.EState.WaitingForProtocolHandshake, Connection.ESubState.None);
            }

            if (connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.pending && connection.SubState == Connection.ESubState.None)
            {
                if (this.connectionHello.prolongationRequest == true)
                {
                    connection.ProlongHelloDeadline();
                    await new ConnectionHelloMessage(ConnectionHelloPhaseType.ready, connection.WaitForReadyTimerValue).Send(connection.WebSocket, logger).ConfigureAwait(false);
                    return (Connection.EState.WaitingForConnectionHello, Connection.ESubState.FirstPending);
                }
                else
                {
                    return (connection.State, connection.SubState); //if we receive a pending hello message that is not a prolongation request, we can ignore it
                }
            }

            if (connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.pending && connection.SubState == Connection.ESubState.FirstPending)
            {
                if (this.connectionHello.prolongationRequest == true)
                {
                    connection.ProlongHelloDeadline();
                    await new ConnectionHelloMessage(ConnectionHelloPhaseType.ready, connection.WaitForReadyTimerValue).Send(connection.WebSocket, logger).ConfigureAwait(false);
                    return (Connection.EState.WaitingForConnectionHello, Connection.ESubState.SecondPending);
                }
                else
                {
                    return (connection.State, connection.SubState); //if we receive a pending hello message that is not a prolongation request, we can ignore it
                }
            }

            if (connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.pending && connection.SubState == Connection.ESubState.SecondPending)
            {
                await UpdateConnectionStatusAsync(connection, DeviceConnectionStatus.Aborted).ConfigureAwait(false);
                await new ConnectionHelloMessage(ConnectionHelloPhaseType.aborted).Send(connection.WebSocket, logger).ConfigureAwait(false);
                return (Connection.EState.Stopped, Connection.ESubState.None);
            }

            if (connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.aborted)
            {
                await UpdateConnectionStatusAsync(connection, DeviceConnectionStatus.Aborted).ConfigureAwait(false);
                return (Connection.EState.Stopped, Connection.ESubState.None);
            }

            throw new Exception("Was waiting for Init");
        }
    }

    [System.SerializableAttribute()]
    public partial class ConnectionHelloType
    {
        public ConnectionHelloPhaseType phase { get; set; }

        public uint? waiting { get; set; }

        public bool? prolongationRequest { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ConnectionHelloPhaseType
    {
        pending,
        ready,
        aborted,
    }
}
