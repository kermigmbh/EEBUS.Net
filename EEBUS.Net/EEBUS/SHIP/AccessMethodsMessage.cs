using System.Text.Json.Serialization;

using EEBUS.Messages;

namespace EEBUS.SHIP.Messages
{
	public class AccessMethodsMessage : ShipControlMessage<AccessMethodsMessage>
	{
		static AccessMethodsMessage()
		{
			Register( new Class() );
		}

		public AccessMethodsMessage()
		{
		}

		public AccessMethodsMessage( string id )
		{
			this.accessMethods.id = id;
		}

		public AccessMethodsType accessMethods { get; set; } = new();

		public override string GetId()
		{
			return this.accessMethods.id;
		}

		public new class Class : ShipControlMessage<AccessMethodsMessage>.Class
		{
			public override AccessMethodsMessage Create(ReadOnlySpan<byte> data, Connection connection )
			{
				return template.FromJsonVirtual( data, connection );
			}
		}

		public override async Task<(Connection.EState, Connection.ESubState)> NextServerState( Connection connection )
		{
			if ( connection.State == Connection.EState.WaitingForAccessMethods )
			{
				await Send( connection.WebSocket ).ConfigureAwait( false );
				return (Connection.EState.Connected, Connection.ESubState.None);
			}

			throw new Exception( "Was waiting for AccessMethods" );
		}

		public override async Task<(Connection.EState, Connection.ESubState)> NextClientState( Connection connection)
		{
			if ( connection.State == Connection.EState.WaitingForAccessMethods )
			{
				return (Connection.EState.Connected, Connection.ESubState.None);
			}

			throw new Exception( "Was waiting for AccessMethods" );
		}
	}

	[System.SerializableAttribute()]
	public class AccessMethodsType
	{
		[JsonPropertyName("id")]
		public string					   id		  { get; set; }

		[JsonPropertyName("dnsSd_mDns")]
		public AccessMethodsTypeDnsSd_mDns? dnsSd_mDns { get; set; }

		[JsonPropertyName("dns")]
		public AccessMethodsTypeDns?		   dns		  { get; set; }
	}

	/// <remarks/>
	[System.SerializableAttribute()]
	public class AccessMethodsTypeDnsSd_mDns
	{
	}

	/// <remarks/>
	[System.SerializableAttribute()]
	public class AccessMethodsTypeDns
	{
		public string uri { get; set; }
	}
}
