using System.Text.Json.Serialization;

using EEBUS.Messages;
using EEBUS.Net.EEBUS.Models;
using Microsoft.Extensions.Logging;

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
            if (phase == ConnectionClosePhaseType.announce)
            {
                this.connectionClose.maxTime = DefaultMaxTimeMs;
            }
        }

        /// <summary>SHIP 13.4.8.1.2 DEV-A rule 1b: time after which we close the connection even without a confirm.</summary>
        public const uint DefaultMaxTimeMs = 1000;

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

        /// <summary>
        /// SHIP 13.4.8.1.2: the server side answers a received close announce with a close confirm and closes the connection afterwards.
        /// Returns <see cref="Connection.EState.Stopped"/> so the receive loop terminates regardless of the websocket state.
        /// </summary>
        public override async Task<(Connection.EState, Connection.ESubState)> NextServerStateAsync(Connection connection, ILogger? logger = null)
        {
            if (!await HandleReceivedCloseAsync(connection, logger).ConfigureAwait(false))
                return (Connection.EState.Disconnected, Connection.ESubState.None);

            //Closing the output lets the server receive loop (which runs while the websocket is open) terminate without an error.
            await connection.CloseAsync().ConfigureAwait(false);
            return (Connection.EState.Stopped, Connection.ESubState.None);
        }

        /// <summary>
        /// SHIP 13.4.8.1.2: the client side answers a received close announce with a close confirm and stops its receive loop, which closes the websocket.
        /// </summary>
        public override async Task<(Connection.EState, Connection.ESubState)> NextClientStateAsync(Connection connection, ILogger? logger = null)
        {
            if (!await HandleReceivedCloseAsync(connection, logger).ConfigureAwait(false))
                return (Connection.EState.Disconnected, Connection.ESubState.None);

            return (Connection.EState.Stopped, Connection.ESubState.None);
        }

        /// <summary>
        /// Processes a received close message. Returns true if this was an announce that has been confirmed and the connection must now be closed,
        /// false if it was a confirm for a close we initiated ourselves (the initiator closes the websocket).
        /// </summary>
        private async Task<bool> HandleReceivedCloseAsync(Connection connection, ILogger? logger)
        {
            if (connectionClose.phase == ConnectionClosePhaseType.confirm)
            {
                logger?.LogDebug("Received close confirm from {remote}", connection.Remote?.Name);
                return false;
            }

            logger?.LogInformation("Received close announce from {remote} (reason: {reason}, maxTime: {maxTime} ms)", connection.Remote?.Name, connectionClose.reason, connectionClose.maxTime);

            if (connection.State == Connection.EState.WaitingForCloseConfirm)
            {
                //Both sides announced a close at the same time (13.4.8.1.2 general rule 1): the first confirm closes the connection, so let our own
                //pending close complete immediately instead of waiting for its maxTime.
                connection.CompletePendingRequest(GetId(), this);
            }

            try
            {
                //DEV-B rule 1: confirm with phase only, no other sub-elements
                CloseMessage confirm = new CloseMessage(ConnectionClosePhaseType.confirm);
                await connection.SendMessageAsync(confirm).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to send close confirm to {remote}", connection.Remote?.Name);
            }

            return true;
        }
    }


    public class ConnectionCloseType
    {
        public ConnectionClosePhaseType phase { get; set; }

        /// <summary>Only present in the announce phase (SHIP 13.4.8.1.2).</summary>
        public uint? maxTime { get; set; }

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
