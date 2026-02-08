using System.Text.Json.Serialization;

using EEBUS.Enums;
using EEBUS.Messages;

namespace EEBUS.SHIP.Messages
{
	public class ProtocolHandshakeMessage : ShipControlMessage<ProtocolHandshakeMessage>
	{
		static ProtocolHandshakeMessage()
		{
			Register( new Class() );
		}

		public ProtocolHandshakeMessage()
		{
		}

		public ProtocolHandshakeMessage( ProtocolHandshakeTypeType handshakeType, ushort major, ushort minor ) 
		{
			this.messageProtocolHandshake.handshakeType = handshakeType;
			this.messageProtocolHandshake.version = new MessageProtocolHandshakeTypeVersion( major, minor );
		}

		public new class Class : ShipControlMessage<ProtocolHandshakeMessage>.Class
		{
			public override ProtocolHandshakeMessage Create( byte[] data, Connection connection )
			{
				return template.FromJsonVirtual( data, connection );
			}
		}

		public MessageProtocolHandshakeType messageProtocolHandshake { get; set; } = new();

		public bool IsEqual( ProtocolHandshakeMessage other )
		{
			return this.messageProtocolHandshake.IsEqual( other.messageProtocolHandshake );
		}

		public override (Connection.EState, Connection.ESubState, string) ServerTest( Connection.EState state )
		{
			string			error		= null;
			Connection.EState	newState	= state;
			Connection.ESubState	newSubState = Connection.ESubState.None;

			if ( state == Connection.EState.WaitingForProtocolHandshake && this.messageProtocolHandshake.handshakeType != ProtocolHandshakeTypeType.announceMax )
			{
				error		= "Protocol version max announcement expected!";
				newState	= Connection.EState.SendProtocolHandshakeError;
				newSubState	= Connection.ESubState.UnexpectedMessage;
			}
			else if ( state == Connection.EState.WaitingForProtocolHandshakeConfirm && this.messageProtocolHandshake.handshakeType != ProtocolHandshakeTypeType.select )
			{
				error		= "Protocol format mismatch!";
				newState	= Connection.EState.SendProtocolHandshakeError;
				newSubState	= Connection.ESubState.FormatMismatch;
			}
			else if ( this.messageProtocolHandshake.version.major != 1 && this.messageProtocolHandshake.version.minor != 0 )
			{
				error		= "Protocol version mismatch!";
				newState	= Connection.EState.SendProtocolHandshakeError;
				newSubState	= Connection.ESubState.FormatMismatch;
			}
			else if ( (this.messageProtocolHandshake.formats.format.Length == 0) || (this.messageProtocolHandshake.formats.format[0] != SHIPMessageFormat.JSON_UTF8) )
			{
				error		= "Protocol format mismatch!";
				newState	= Connection.EState.SendProtocolHandshakeError;
				newSubState	= Connection.ESubState.FormatMismatch;
			}

			return (newState, newSubState, error);
		}

		public override async Task<(Connection.EState, Connection.ESubState)> NextServerState( Connection connection )
		{
			if ( connection.State == Connection.EState.WaitingForProtocolHandshake )
			{
				this.messageProtocolHandshake.handshakeType = ProtocolHandshakeTypeType.select;
				await Send( connection.WebSocket ).ConfigureAwait( false );
				return (Connection.EState.SendProtocolHandshakeConfirm, Connection.ESubState.None);
			}
			else if ( connection.State == Connection.EState.SendProtocolHandshakeConfirm )
			{
				return (Connection.EState.WaitingForPinCheck, Connection.ESubState.None);
			}
			else if ( connection.State == Connection.EState.SendProtocolHandshakeError && connection.SubState == Connection.ESubState.FormatMismatch )
			{
				ProtocolHandshakeErrorMessage message = new ProtocolHandshakeErrorMessage( SHIPHandshakeError.SELECTION_MISMATCH );
				await message.Send( connection.WebSocket ).ConfigureAwait( false );
				return await message.NextServerState( connection ).ConfigureAwait( false );
			}
			else if ( connection.State == Connection.EState.SendProtocolHandshakeError && connection.SubState == Connection.ESubState.UnexpectedMessage )
			{
				ProtocolHandshakeErrorMessage message = new ProtocolHandshakeErrorMessage( SHIPHandshakeError.UNEXPECTED_MESSAGE );
				await message.Send( connection.WebSocket ).ConfigureAwait( false );
				return await message.NextServerState( connection ).ConfigureAwait( false );
			}

			throw new Exception( "ProtocolHandshake aborted!" );
		}

		public override (Connection.EState, Connection.ESubState, string) ClientTest( Connection.EState state )
		{
			string			error		= null;
			Connection.EState	newState	= state;
			Connection.ESubState newSubState	= Connection.ESubState.None;

			if ( state == Connection.EState.WaitingForProtocolHandshake && this.messageProtocolHandshake.handshakeType != ProtocolHandshakeTypeType.select )
			{
				error		= "Protocol version selection expected!";
				newState	= Connection.EState.SendProtocolHandshakeError;
				newSubState	= Connection.ESubState.UnexpectedMessage;
			}
			else if (state == Connection.EState.WaitingForProtocolHandshakeConfirm && this.messageProtocolHandshake.handshakeType != ProtocolHandshakeTypeType.select)
			{
				error		= "Protocol format mismatch!";
				newState	= Connection.EState.SendProtocolHandshakeError;
				newSubState	= Connection.ESubState.FormatMismatch;
			}
			else if ( this.messageProtocolHandshake.version.major != 1 && this.messageProtocolHandshake.version.minor != 0 )
			{
				error		= "Protocol version mismatch!";
				newState	= Connection.EState.SendProtocolHandshakeError;
				newSubState	= Connection.ESubState.FormatMismatch;
			}
			else if ((this.messageProtocolHandshake.formats.format.Length == 0) || (this.messageProtocolHandshake.formats.format[0] != SHIPMessageFormat.JSON_UTF8))
			{
				error		= "Protocol format mismatch!";
				newState	= Connection.EState.SendProtocolHandshakeError;
				newSubState	= Connection.ESubState.FormatMismatch;
			}
			else
			{
				newState	= Connection.EState.SendProtocolHandshakeConfirm;
			}
			
			return (newState, newSubState, error);
		}

		public override async Task<(Connection.EState, Connection.ESubState)> NextClientState( Connection connection )
		{
			if ( connection.State == Connection.EState.SendProtocolHandshakeConfirm)
			{
				this.messageProtocolHandshake.handshakeType = ProtocolHandshakeTypeType.select;
				await Send( connection.WebSocket ).ConfigureAwait( false );

				PinCheckMessage message = new PinCheckMessage( PinStateType.none );
				await message.Send( connection.WebSocket ).ConfigureAwait( false );
				return (Connection.EState.WaitingForPinCheck, Connection.ESubState.None);
			}
			else if ( connection.State == Connection.EState.SendProtocolHandshakeError && connection.SubState == Connection.ESubState.FormatMismatch )
			{
				ProtocolHandshakeErrorMessage message = new ProtocolHandshakeErrorMessage( SHIPHandshakeError.SELECTION_MISMATCH );
				await message.Send( connection.WebSocket ).ConfigureAwait( false );
				return await message.NextClientState( connection ).ConfigureAwait( false );
			}
			else if ( connection.State == Connection.EState.SendProtocolHandshakeError && connection.SubState == Connection.ESubState.UnexpectedMessage )
			{
				ProtocolHandshakeErrorMessage message = new ProtocolHandshakeErrorMessage( SHIPHandshakeError.UNEXPECTED_MESSAGE );
				await message.Send( connection.WebSocket ).ConfigureAwait( false );
				return await message.NextClientState( connection ).ConfigureAwait( false );
			}

			throw new Exception( "ProtocolHandshake aborted!" );
		}
	}

	[System.SerializableAttribute()]
	public class MessageProtocolHandshakeType
	{
		public ProtocolHandshakeTypeType		   handshakeType { get; set; } = new();

		public MessageProtocolHandshakeTypeVersion version		 { get; set; } = new();

		public MessageProtocolHandshakeTypeFormats formats		 { get; set; } = new( SHIPMessageFormat.JSON_UTF8 );

		public bool IsEqual( MessageProtocolHandshakeType other )
		{
			if ( this.handshakeType != other.handshakeType )
				return false;

			if ( ! this.version.IsEqual( other.version ) )
				return false;

			if ( ! this.formats.IsEqual( other.formats ) )
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

		public MessageProtocolHandshakeTypeVersion( ushort major, ushort minor )
		{
			this.major = major;
			this.minor = minor;
		}

		public ushort major { get; set; }

		public ushort minor { get; set; }

		public bool IsEqual( MessageProtocolHandshakeTypeVersion other )
		{
			if ( this.major != other.major )
				return false;

			if ( this.minor != other.minor )
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

		public MessageProtocolHandshakeTypeFormats( string format )
		{
			this.format = new string[] { format };
		}

		public string[] format { get; set; }

		public bool IsEqual( MessageProtocolHandshakeTypeFormats other )
		{
			if ( this.format.Length != other.format.Length )
				return false;

			for ( int i = 0; i < this.format.Length; i++ )
				if ( this.format[i] != other.format[i] )
					return false;

			return true;
		}
	}
}
