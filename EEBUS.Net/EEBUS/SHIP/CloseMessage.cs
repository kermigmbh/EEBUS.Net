using System.Text.Json.Serialization;

using EEBUS.Messages;

namespace EEBUS.SHIP.Messages
{
	public class CloseMessage : ShipEndMessage<CloseMessage>
	{
		static CloseMessage()
		{
			Register( new Class() );
		}

		public CloseMessage()
		{
		}

		public CloseMessage( ConnectionClosePhaseType phase )
		{
			this.connectionClose[0].phase = phase;
		}

		public new class Class : ShipEndMessage<CloseMessage>.Class
		{
			public override CloseMessage Create(ReadOnlySpan<byte> data/*, Connection connection*/ )
			{
				return template.FromJsonVirtual( data/*, connection*/ );
			}
		}

		public ConnectionCloseType[] connectionClose { get; set; } = [new()];
	}

	 
	public class ConnectionCloseType
	{
		public ConnectionClosePhaseType	 phase			  { get; set; }

		public uint						 maxTime		  { get; set; }

		public bool						 maxTimeSpecified { get; set; }

		public ConnectionCloseReasonType reason			  { get; set; }

		public bool						 reasonSpecified  { get; set; }
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
