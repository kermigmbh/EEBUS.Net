using EEBUS.Messages;

namespace EEBUS.SHIP.Messages
{
	public class ProtocolHandshakeErrorMessage : ShipControlMessage<ProtocolHandshakeErrorMessage>
	{
		static ProtocolHandshakeErrorMessage()
		{
			Register( new Class() );
		}

		public ProtocolHandshakeErrorMessage()
		{
		}

		public ProtocolHandshakeErrorMessage( byte error )
		{
			this.messageProtocolHandshakeError.error = error;
		}

		public new class Class : ShipControlMessage<ProtocolHandshakeErrorMessage>.Class
		{
			public override ProtocolHandshakeErrorMessage Create(ReadOnlySpan<byte> data/*, Connection connection*/ )
			{
				return template.FromJsonVirtual( data/*, connection*/ );
			}
		}

		public MessageProtocolHandshakeErrorType messageProtocolHandshakeError { get; set; } = new();

		public override async Task<(Connection.EState, Connection.ESubState)> NextServerState( Connection connection )
		{
			return (Connection.EState.Stopped, Connection.ESubState.None);
		}

		public override async Task<(Connection.EState, Connection.ESubState)> NextClientState( Connection connection )
		{
			return (Connection.EState.Stopped, Connection.ESubState.None);
		}
	}

	[System.SerializableAttribute()]
	public partial class MessageProtocolHandshakeErrorType
	{
		public byte error { get; set; }
	}
}
