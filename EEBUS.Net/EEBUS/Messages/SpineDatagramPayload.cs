using System.Text.Json;
using System.Text.Json.Nodes;

namespace EEBUS.Messages
{
	public class SpineDatagramPayload
	{
		public SpineDatagramPayload()
		{
		}

		public DatagramType datagram { get; set; } = new();

			// Helper to create a JsonNode payload from an arbitrary object using the same options
			public static JsonNode CreateJsonPayload( object value )
			{
				var options = new JsonSerializerOptions
				{
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
					DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
				};
				options.Converters.Add( new System.Text.Json.Serialization.JsonStringEnumConverter() );
				return JsonSerializer.SerializeToNode( value, options );
			}

		private string GetAnswerCmdClassifier()
		{
			switch ( this.datagram.header.cmdClassifier )
			{
				case "read":
					return "reply";
				case "call":
					return "result";
				case "notify":
					return "";
				case "write":
					return "result";
				default:
					return null;
			}
		}

		private SpineCmdPayloadBase.Class GetClass()
		{
			if ( this.datagram.payload is not JsonObject root )
				return null;
			if ( !root.TryGetPropertyValue( "cmd", out JsonNode cmds ) || cmds is not JsonArray cmdArray || cmdArray.Count == 0 )
				return null;

			JsonNode firstCmdNode = cmdArray[0];
			if ( firstCmdNode is not JsonObject cmd )
				return null;

			KeyValuePair<string, JsonNode?> prop = cmd.First();
			string command = prop.Key;
			if ( command == "function" && prop.Value is not null )
				command = prop.Value.GetValue<string>();

			SpineCmdPayloadBase.Class cls = SpineCmdPayloadBase.GetClass( command );
			if ( null == cls )
				return null;

			return cls;
		}

		public SpineDatagramPayload CreateAnswer( ulong counter, Connection connection )
		{
			SpineCmdPayloadBase.Class cls = GetClass();
			if ( null == cls )
				return null;

			SpineDatagramPayload reply = new SpineDatagramPayload();
			reply.datagram.header.addressSource		   = this.datagram.header.addressDestination;
			reply.datagram.header.addressSource.device = connection.Local.DeviceId;
			reply.datagram.header.addressDestination   = this.datagram.header.addressSource;
			reply.datagram.header.msgCounter		   = counter;
			reply.datagram.header.msgCounterReference  = this.datagram.header.msgCounter;
			reply.datagram.header.cmdClassifier		   = GetAnswerCmdClassifier();
			reply.datagram.header.ackRequest		   = cls.GetAnswerAckRequest();

			SpineCmdPayloadBase payload = cls.CreateAnswer( this.datagram, reply.datagram.header, connection );
			if ( null == payload )
				return null;

			reply.datagram.payload = CreateJsonPayload( payload );

			return reply;
		}

		public void Evaluate( Connection connection )
		{
			SpineCmdPayloadBase.Class cls = GetClass();
			if ( null == cls )
				return;

			cls.Evaluate( connection, this.datagram );
		}
	}

	[System.SerializableAttribute()]
	public class DatagramType
	{
		public HeaderType header  { get; set; } = new();

			public JsonNode  payload { get; set; }
	}

	[System.SerializableAttribute()]
	public class HeaderType
	{
		public string	   specificationVersion { get; set; } = "1.3.0";

		public AddressType addressSource		{ get; set; }

		public AddressType addressDestination	{ get; set; }

			public ulong	   msgCounter			{ get; set; }

			public ulong?	   msgCounterReference	{ get; set; }

			public string	   cmdClassifier		{ get; set; }

			public bool?	   ackRequest			{ get; set; }
	}

	[System.SerializableAttribute()]
	public class AddressType
	{
			public string device  { get; set; }

			public int[]  entity  { get; set; }

			public int?	  feature { get; set; }
	}
}
