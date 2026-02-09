using EEBUS.Enums;
using EEBUS.Messages;

namespace EEBUS.SHIP.Messages
{
	public class InitMessage : ShipInitMessage<InitMessage>
	{
		static InitMessage()
		{
			Register( new Class() );
		}

		public InitMessage()
		{
		}

		public new class Class : ShipInitMessage<InitMessage>.Class
		{
			public override InitMessage Create(ReadOnlySpan<byte> data, Connection connection )
			{
				return template.FromJsonVirtual( data, connection );
			}
		}

		public override byte[] bytes { get; set; } = { SHIPMessageType.INIT, SHIPMessageValue.CMI_HEAD };


		public override (Connection.EState, Connection.ESubState, string) ServerTest( Connection.EState state )
		{
			string		 error	  = null;
			Connection.EState newState = state;

			if ( this.bytes[1] != SHIPMessageValue.CMI_HEAD )
			{
				error = "Expected SMI_HEAD payload in INIT message!";
				newState = Connection.EState.Stopped;
			}

			return (newState, Connection.ESubState.None, error);
		}

		public override async Task<(Connection.EState, Connection.ESubState)> NextServerState( Connection connection )
		{
			if ( connection.State == Connection.EState.Disconnected || connection.State == Connection.EState.Connected )
			{
				await Send( connection.WebSocket ).ConfigureAwait( false );
				return (Connection.EState.WaitingForConnectionHello, Connection.ESubState.None);
			}

			throw new Exception( "Was waiting for Init" );
		}

		public override (Connection.EState, Connection.ESubState, string) ClientTest( Connection.EState state )
		{
			string		 error	  = null;
			Connection.EState newState = state;

			if ( this.bytes[1] != SHIPMessageValue.CMI_HEAD )
			{
				error = "Expected SMI_HEAD payload in INIT message!";
				newState = Connection.EState.Stopped;
			}

			return (newState, Connection.ESubState.None, error);
		}

		public override async Task<(Connection.EState, Connection.ESubState)> NextClientState( Connection connection )
		{
			if ( connection.State == Connection.EState.Disconnected || connection.State == Connection.EState.Connected )
			{
				ConnectionHelloMessage message = new ConnectionHelloMessage( ConnectionHelloPhaseType.ready, 60000 );
				await message.Send( connection.WebSocket ).ConfigureAwait( false );

				return (Connection.EState.WaitingForConnectionHello, Connection.ESubState.None);
			}

			throw new Exception( "Was waiting for Init" );
		}
	}
}
