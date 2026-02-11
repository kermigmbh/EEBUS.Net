using EEBUS.Messages;

namespace EEBUS.SHIP.Messages
{
	public class AccessMethodsRequestMessage : ShipControlMessage<AccessMethodsRequestMessage>
	{
		static AccessMethodsRequestMessage()
		{
			Register( new Class() );
		}

		public AccessMethodsRequestMessage()
		{
		}

		public new class Class : ShipControlMessage<AccessMethodsRequestMessage>.Class
		{
			public override AccessMethodsRequestMessage Create(ReadOnlySpan<byte> data/*, Connection connection*/ )
			{
				return template.FromJsonVirtual( data/*, connection*/ );
			}
		}

		public AccessMethodsRequestType accessMethodsRequest { get; set; } = new();
	
		public override async Task<(Connection.EState, Connection.ESubState)> NextServerState( Connection connection )
		{
			if ( connection.State == Connection.EState.WaitingForAccessMethodsRequest )
			{
				await Send( connection.WebSocket ).ConfigureAwait( false );
				return (Connection.EState.WaitingForAccessMethods, Connection.ESubState.None);
			}

			throw new Exception( "Was waiting for AccessMethodsRequest" );
		}

		public override async Task<(Connection.EState, Connection.ESubState)> NextClientState( Connection connection )
		{
			if ( connection.State == Connection.EState.WaitingForAccessMethodsRequest )
			{
				AccessMethodsMessage method = new AccessMethodsMessage( connection.Local.DeviceId );
				await method.Send( connection.WebSocket ).ConfigureAwait( false );

				return (Connection.EState.WaitingForAccessMethods, Connection.ESubState.None);
			}

			throw new Exception( "Was waiting for PinChecked" );
		}
	}

	[System.SerializableAttribute()]
	public class AccessMethodsRequestType
	{
	}
}
