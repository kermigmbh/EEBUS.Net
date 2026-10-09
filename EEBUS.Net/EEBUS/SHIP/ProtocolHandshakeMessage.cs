using System.Text.Json.Serialization;

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
    public class ProtocolHandshakeMessage : ShipControlMessage<ProtocolHandshakeMessage>
    {
        static ProtocolHandshakeMessage()
        {
            Register(new Class());
        }

        public ProtocolHandshakeMessage()
        {
        }

        public ProtocolHandshakeMessage(ProtocolHandshakeTypeType handshakeType, ushort major, ushort minor)
        {
            this.messageProtocolHandshake.handshakeType = handshakeType;
            this.messageProtocolHandshake.version = new MessageProtocolHandshakeTypeVersion(major, minor);
        }

        public new class Class : ShipControlMessage<ProtocolHandshakeMessage>.Class
        {
            public override ProtocolHandshakeMessage Create(ReadOnlySpan<byte> data/*, Connection connection*/ )
            {
                return template.FromJsonVirtual(data/*, connection*/ );
            }
        }

        public MessageProtocolHandshakeType messageProtocolHandshake { get; set; } = new();

        public bool IsEqual(ProtocolHandshakeMessage other)
        {
            return this.messageProtocolHandshake.IsEqual(other.messageProtocolHandshake);
        }

        public override Task<(Connection.EState, Connection.ESubState, string)> ServerTestAsync(Connection.EState state, Connection? connection = null, ILogger? logger = null)
        {
            string error = string.Empty;
            Connection.EState newState = state;
            Connection.ESubState newSubState = Connection.ESubState.None;

            if (state == Connection.EState.WaitingForProtocolHandshake && this.messageProtocolHandshake.handshakeType != ProtocolHandshakeTypeType.announceMax)
            {
                //In this state, we expect the peer to send a handshake message where they announce their maximum supported SHIP version and the supported text formats. If they send a different type of handshake message, we consider it an error.
                error = "Protocol version max announcement expected!";
                newState = Connection.EState.SendProtocolHandshakeError;
                newSubState = Connection.ESubState.UnexpectedMessage;
            }

            //TODO: the state WaitingForProtocolHandshakeConfirm is not used, check if we still need this guard
            else if (state == Connection.EState.WaitingForProtocolHandshakeConfirm && this.messageProtocolHandshake.handshakeType != ProtocolHandshakeTypeType.select)
            {
                error = "Protocol format mismatch!";
                newState = Connection.EState.SendProtocolHandshakeError;
                newSubState = Connection.ESubState.FormatMismatch;
            }
            else if (state == Connection.EState.WaitingForProtocolHandshake
                ? this.messageProtocolHandshake.version.CompareTo(new MessageProtocolHandshakeTypeVersion(SHIPVersion.MIN_MAJOR, SHIPVersion.MIN_MINOR)) < 0
                : !this.messageProtocolHandshake.version.IsSupported())
            {
                //The peer answered our proposal with a verson we do not support
                error = "Protocol version mismatch!";
                newState = Connection.EState.SendProtocolHandshakeError;
                newSubState = Connection.ESubState.FormatMismatch;
            }
            else if ((this.messageProtocolHandshake.formats.format.Length == 0) || (this.messageProtocolHandshake.formats.format[0] != SHIPMessageFormat.JSON_UTF8))
            {
                //The peer answered our proposal with a verson we do not support
                error = "Protocol format mismatch!";
                newState = Connection.EState.SendProtocolHandshakeError;
                newSubState = Connection.ESubState.FormatMismatch;
            }

            return Task.FromResult((newState, newSubState, error));
        }

        /*After the connection hello message, we wait for the peer's protocol handshake message, where they announce their maximum supported SHIP version and the supported text formats.
          We then select the maximum version supported by both partners and send back a protocol handshake message with the selected version and text format.
          Example:
            1. Peer sends a handshake message announcing max version 1.1 and format UTF-8. That means the peer supports versions 1.0 and 1.1, and the only supported text format is UTF-8.
            2. Since we (currently) only support version 1.0, we send a protocol handshake message with version 1.0 and format UTF-8
               If the peer accepts this selection, they will send back the message we sent
        */
        public override async Task<(Connection.EState, Connection.ESubState)> NextServerStateAsync(Connection connection, ILogger? logger = null)
        {
            if (connection.State == Connection.EState.WaitingForProtocolHandshake)
            {
                //We select the highest version supported by both us and the peer and send back a select message
                MessageProtocolHandshakeTypeVersion selected = MessageProtocolHandshakeTypeVersion.SelectCommon(this.messageProtocolHandshake.version);
                await new ProtocolHandshakeMessage(ProtocolHandshakeTypeType.select, selected.major, selected.minor).Send(connection.WebSocket, logger).ConfigureAwait(false);
                return (Connection.EState.SendProtocolHandshakeConfirm, Connection.ESubState.None);
            }
            else if (connection.State == Connection.EState.SendProtocolHandshakeConfirm)
            {
                //The peer confirmed our proposed selection by sending our message back, so we can enter the next SME state
                return (Connection.EState.WaitingForPinCheck, Connection.ESubState.None);
            }
            else if (connection.State == Connection.EState.SendProtocolHandshakeError && connection.SubState == Connection.ESubState.FormatMismatch)
            {
                //The peer answered our proposal with a version we do not support, so we send back an error message and close the connection
                ProtocolHandshakeErrorMessage message = new ProtocolHandshakeErrorMessage(SHIPHandshakeError.SELECTION_MISMATCH);
                await message.Send(connection.WebSocket, logger).ConfigureAwait(false);
                return (Connection.EState.Stopped, Connection.ESubState.None);
            }
            else if (connection.State == Connection.EState.SendProtocolHandshakeError && connection.SubState == Connection.ESubState.UnexpectedMessage)
            {
                //The peer sent a handshake message we did not expect in the current state, so we send back an error message and close the connection
                ProtocolHandshakeErrorMessage message = new ProtocolHandshakeErrorMessage(SHIPHandshakeError.UNEXPECTED_MESSAGE);
                await message.Send(connection.WebSocket, logger).ConfigureAwait(false);
                return (Connection.EState.Stopped, Connection.ESubState.None);
            }

            throw new Exception("ProtocolHandshake aborted!");
        }

        public override Task<(Connection.EState, Connection.ESubState, string)> ClientTestAsync(Connection.EState state, Connection? connection = null, ILogger? logger = null)
        {
            string error = string.Empty;
            Connection.EState newState = state;
            Connection.ESubState newSubState = Connection.ESubState.None;

            if (state == Connection.EState.WaitingForProtocolHandshake && this.messageProtocolHandshake.handshakeType != ProtocolHandshakeTypeType.select)
            {
                //In this state, we expect the peer to send a handshake message where they select the version and format we announced in our previous handshake message. If they send a different type of handshake message, we consider it an error.
                error = "Protocol version selection expected!";
                newState = Connection.EState.SendProtocolHandshakeError;
                newSubState = Connection.ESubState.UnexpectedMessage;
            }
            //TODO: the state WaitingForProtocolHandshakeConfirm is not used, check if we still need this guard
            else if (state == Connection.EState.WaitingForProtocolHandshakeConfirm && this.messageProtocolHandshake.handshakeType != ProtocolHandshakeTypeType.select)
            {
                error = "Protocol format mismatch!";
                newState = Connection.EState.SendProtocolHandshakeError;
                newSubState = Connection.ESubState.FormatMismatch;
            }
            else if (!this.messageProtocolHandshake.version.IsSupported())
            {
                //The peer selected a version we do not support
                error = "Protocol version mismatch!";
                newState = Connection.EState.SendProtocolHandshakeError;
                newSubState = Connection.ESubState.FormatMismatch;
            }
            else if ((this.messageProtocolHandshake.formats.format.Length == 0) || (this.messageProtocolHandshake.formats.format[0] != SHIPMessageFormat.JSON_UTF8))
            {
                //The peer selected a format we do not support
                error = "Protocol format mismatch!";
                newState = Connection.EState.SendProtocolHandshakeError;
                newSubState = Connection.ESubState.FormatMismatch;
            }
            else
            {
                newState = Connection.EState.SendProtocolHandshakeConfirm;
            }

            return Task.FromResult((newState, newSubState, error));
        }

        /*
         * We received an answer to our protocol handshake message where the peer selected the version and format they support
         */
        public override async Task<(Connection.EState, Connection.ESubState)> NextClientStateAsync(Connection connection, ILogger? logger = null)
        {
            if (connection.State == Connection.EState.SendProtocolHandshakeConfirm)
            {
                //We confirm the peer's selection by sending back the same message we received, and then we enter the next SME state by sending a PinCheckMessage
                this.messageProtocolHandshake.handshakeType = ProtocolHandshakeTypeType.select;
                await Send(connection.WebSocket, logger).ConfigureAwait(false);

                //We do not implement a PIN requirement, so we send a missage with PinStateType none, indicating we do not require a pin
                PinCheckMessage message = new PinCheckMessage(PinStateType.none);
                await message.Send(connection.WebSocket, logger).ConfigureAwait(false);
                return (Connection.EState.WaitingForPinCheck, Connection.ESubState.None);
            }
            else if (connection.State == Connection.EState.SendProtocolHandshakeError && connection.SubState == Connection.ESubState.FormatMismatch)
            {
                //The peer selected an invalid version, e.g. a version we do not support, so we send back an error message and close the connection
                ProtocolHandshakeErrorMessage message = new ProtocolHandshakeErrorMessage(SHIPHandshakeError.SELECTION_MISMATCH);
                await message.Send(connection.WebSocket, logger).ConfigureAwait(false);
                return (Connection.EState.Stopped, Connection.ESubState.None);
            }
            else if (connection.State == Connection.EState.SendProtocolHandshakeError && connection.SubState == Connection.ESubState.UnexpectedMessage)
            {
                //The peer sent a handshake message we did not expect in the current state, so we send back an error message and close the connection
                ProtocolHandshakeErrorMessage message = new ProtocolHandshakeErrorMessage(SHIPHandshakeError.UNEXPECTED_MESSAGE);
                await message.Send(connection.WebSocket, logger).ConfigureAwait(false);
                return (Connection.EState.Stopped, Connection.ESubState.None);
            }

            throw new Exception("ProtocolHandshake aborted!");
        }
    }

    [System.SerializableAttribute()]
    public class MessageProtocolHandshakeType
    {
        public ProtocolHandshakeTypeType handshakeType { get; set; } = new();

        public MessageProtocolHandshakeTypeVersion version { get; set; } = new();

        public MessageProtocolHandshakeTypeFormats formats { get; set; } = new(SHIPMessageFormat.JSON_UTF8);

        public bool IsEqual(MessageProtocolHandshakeType other)
        {
            if (this.handshakeType != other.handshakeType)
                return false;

            if (!this.version.IsEqual(other.version))
                return false;

            if (!this.formats.IsEqual(other.formats))
                return false;

            return true;
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ProtocolHandshakeTypeType
    {
        announceMax,
        select,
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    public class MessageProtocolHandshakeTypeVersion
    {
        public MessageProtocolHandshakeTypeVersion()
        {
        }

        public MessageProtocolHandshakeTypeVersion(ushort major, ushort minor)
        {
            this.major = major;
            this.minor = minor;
        }

        public ushort major { get; set; }

        public ushort minor { get; set; }

        public static MessageProtocolHandshakeTypeVersion Max => new(SHIPVersion.MAX_MAJOR, SHIPVersion.MAX_MINOR);

        public int CompareTo(MessageProtocolHandshakeTypeVersion other)
        {
            int result = this.major.CompareTo(other.major);
            return result != 0 ? result : this.minor.CompareTo(other.minor);
        }

        /// <summary>
        /// True if this version lies within the range supported by this stack, i.e. it is greater than the min supported version and less than or equal to the max supported version
        /// </summary>
        public bool IsSupported()
        {
            return CompareTo(new MessageProtocolHandshakeTypeVersion(SHIPVersion.MIN_MAJOR, SHIPVersion.MIN_MINOR)) >= 0
                && CompareTo(Max) <= 0;
        }

        /// <summary>
        /// Highest version supported by both this stack and a peer announcing <paramref name="peerMax"/> (spec line 2390).
        /// </summary>
        public static MessageProtocolHandshakeTypeVersion SelectCommon(MessageProtocolHandshakeTypeVersion peerMax)
        {
            return peerMax.CompareTo(Max) < 0 ? new(peerMax.major, peerMax.minor) : Max;
        }

        public bool IsEqual(MessageProtocolHandshakeTypeVersion other)
        {
            if (this.major != other.major)
                return false;

            if (this.minor != other.minor)
                return false;

            return true;
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    public class MessageProtocolHandshakeTypeFormats
    {
        public MessageProtocolHandshakeTypeFormats()
        {
        }

        public MessageProtocolHandshakeTypeFormats(string format)
        {
            this.format = new string[] { format };
        }

        public string[] format { get; set; }

        public bool IsEqual(MessageProtocolHandshakeTypeFormats other)
        {
            if (this.format.Length != other.format.Length)
                return false;

            for (int i = 0; i < this.format.Length; i++)
                if (this.format[i] != other.format[i])
                    return false;

            return true;
        }
    }
}
