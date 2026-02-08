using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EEBUS.Messages
{
	public class SpineDatagramPayload
	{
		public SpineDatagramPayload()
		{
		}

		public DatagramType datagram { get; set; } = new();

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

		private SpineCmdPayloadBase.Class? GetClass()
		{
			// payload is a JsonNode representing the datagram body
			if (this.datagram.payload is not JsonObject payloadObj)
				return null;
			if (!payloadObj.TryGetPropertyValue("cmd", out JsonNode? cmdsNode))
				return null;
			if (cmdsNode is not JsonArray cmdsArray || cmdsArray.Count == 0)
				return null;

			if (cmdsArray[0] is not JsonObject cmdObj)
				return null;

			// take first property of the command object
			var prop = cmdObj.FirstOrDefault();
			if (prop.Equals(default(KeyValuePair<string, JsonNode?>)))
				return null;

			string command = prop.Key;
			if (command == "function" && prop.Value is JsonValue v && v.TryGetValue<string>(out var fn))
				command = fn;

			SpineCmdPayloadBase.Class cls = SpineCmdPayloadBase.GetClass( command );
			if ( null == cls )
				return null;

			return cls;
		}

		public SpineDatagramPayload? CreateAnswer( ulong counter, Connection connection )
		{
			SpineCmdPayloadBase.Class? cls = GetClass();
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

			reply.datagram.payload = payload.ToJsonNode();//JsonSerializer.SerializeToNode(payload);

			return reply;
		}

		public void Evaluate( Connection connection )
		{
			SpineCmdPayloadBase.Class? cls = GetClass();
			if ( null == cls )
				return;

			cls.Evaluate( connection, this.datagram );
		}
	}

	[System.SerializableAttribute()]
	public class DatagramType
	{
		public HeaderType header  { get; set; } = new();

		public JsonNode? payload { get; set; }
	}

	[System.SerializableAttribute()]
	public class HeaderType
	{
		public string	   specificationVersion { get; set; } = "1.3.0";

		public AddressType addressSource		{ get; set; }

		public AddressType addressDestination	{ get; set; }

		public ulong	   msgCounter			{ get; set; }

		[JsonPropertyName("msgCounterReference")]
		public ulong?	   msgCounterReference	{ get; set; }

		public string	   cmdClassifier		{ get; set; }

		[JsonPropertyName("ackRequest")]
		public bool?	   ackRequest			{ get; set; }
	}

	[System.SerializableAttribute()]
	public class AddressType
	{
		[JsonPropertyName("device")]
		public string? device  { get; set; }

		public int[]  entity  { get; set; }

		[JsonPropertyName("feature")]
		public int?	  feature { get; set; }
	}
}
