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
			public override CloseMessage Create( byte[] data, Connection connection )
			{
				return template.FromJsonVirtual( data, connection );
			}
		}

		public ConnectionCloseType[] connectionClose { get; set; } = [new()];
	}

	[System.SerializableAttribute()]
	public class ConnectionCloseType
	{
		public ConnectionClosePhaseType	 phase			  { get; set; }

		public uint						 maxTime		  { get; set; }

		public bool						 maxTimeSpecified { get; set; }

		public ConnectionCloseReasonType reason			  { get; set; }

		public bool						 reasonSpecified  { get; set; }
	}

	[System.SerializableAttribute()]
		[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum ConnectionClosePhaseType
	{
		announce,
		confirm,
	}

	/// <remarks/>
	[System.SerializableAttribute()]
		[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum ConnectionCloseReasonType
	{
		unspecific,
		removedConnection,
	}
}
