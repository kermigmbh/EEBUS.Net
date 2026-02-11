using System.Text.Json.Serialization;

using EEBUS.Messages;

namespace EEBUS.SHIP.Messages
{
	public class ConnectionHelloMessage : ShipControlMessage<ConnectionHelloMessage>
	{
		static ConnectionHelloMessage()
		{
			Register( new Class() );
		}

		public ConnectionHelloMessage()
		{
		}

		public ConnectionHelloMessage( ConnectionHelloPhaseType phase )
		{
			this.connectionHello.phase = phase;
		}

		public ConnectionHelloMessage( ConnectionHelloPhaseType phase, uint waiting )
		{
			this.connectionHello.phase   = phase;
			this.connectionHello.waiting = waiting;
		}

		public new class Class : ShipControlMessage<ConnectionHelloMessage>.Class
		{
			public override ConnectionHelloMessage Create(ReadOnlySpan<byte> data/*, Connection connection*/ )
			{
				return template.FromJsonVirtual( data/*, connection*/ );
			}
		}

		public ConnectionHelloType connectionHello { get; set; } = new();

		public override async Task<(Connection.EState, Connection.ESubState)> NextServerState( Connection connection )
		{
			if ( connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.ready )
			{
				await Send( connection.WebSocket ).ConfigureAwait( false ) ;
				return (Connection.EState.WaitingForProtocolHandshake, Connection.ESubState.None);
			}
			
			if ( connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.pending && connection.SubState == Connection.ESubState.None )
			{
				await Send( connection.WebSocket ).ConfigureAwait( false ) ;
				return (Connection.EState.WaitingForProtocolHandshake, Connection.ESubState.FirstPending);
			}

			if ( connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.pending && connection.SubState == Connection.ESubState.FirstPending )
			{
				await Send( connection.WebSocket ).ConfigureAwait( false );
				return (Connection.EState.WaitingForProtocolHandshake, Connection.ESubState.SecondPending);
			}

			if ( connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.pending && connection.SubState == Connection.ESubState.SecondPending )
			{
				this.connectionHello.phase = ConnectionHelloPhaseType.aborted;
				await Send( connection.WebSocket ).ConfigureAwait( false );
				return (Connection.EState.Stopped, Connection.ESubState.None);
			}

			if ( connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.aborted )
				return (Connection.EState.Stopped, Connection.ESubState.None);

			throw new Exception( "Hello aborted!" );
		}

		public override async Task<(Connection.EState, Connection.ESubState)> NextClientState( Connection connection )
		{
			if ( connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.ready )
			{

				ProtocolHandshakeMessage message = new ProtocolHandshakeMessage( ProtocolHandshakeTypeType.announceMax, 1, 0 );
				await message.Send( connection.WebSocket ).ConfigureAwait( false );
				return (Connection.EState.WaitingForProtocolHandshake, Connection.ESubState.None);
			}

			if ( connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.pending && this.connectionHello.prolongationRequest && connection.SubState == Connection.ESubState.None )
			{
				await Resend( connection.WebSocket ).ConfigureAwait( false ) ;
				return (Connection.EState.WaitingForConnectionHello, Connection.ESubState.FirstPending);
			}

			if ( connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.pending && this.connectionHello.prolongationRequest && connection.SubState == Connection.ESubState.FirstPending )
			{
				await Resend( connection.WebSocket ).ConfigureAwait( false );
				return (Connection.EState.WaitingForConnectionHello, Connection.ESubState.SecondPending);
			}

			if ( connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.pending && connection.SubState == Connection.ESubState.SecondPending )
				return (Connection.EState.Stopped, Connection.ESubState.None);

			if ( connection.State == Connection.EState.WaitingForConnectionHello && this.connectionHello.phase == ConnectionHelloPhaseType.aborted )
				return (Connection.EState.Stopped, Connection.ESubState.None);

			throw new Exception( "Was waiting for Init" );
		}
	}

	[System.SerializableAttribute()]
	public partial class ConnectionHelloType
	{
		public ConnectionHelloPhaseType phase				{ get; set; }

		public uint						waiting				{ get; set; }

		public bool						prolongationRequest	{ get; set; }
	}

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ConnectionHelloPhaseType
	{
		pending,
		ready,
		aborted,
	}
}
