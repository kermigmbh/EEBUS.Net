using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

		private SpineCmdPayloadBase.Class GetClass()
		{
			if ( ! this.datagram.payload.TryGetValue( "cmd", out JToken cmds ) )
				return null;
			if ( ! (cmds is JArray) )
				return null;

			JObject cmd = (cmds as JArray)[0] as JObject;
			if ( null == cmd )
				return null;

			JProperty prop = cmd.Properties().FirstOrDefault();
			if ( null == prop )
				return null;

			string command = prop.Name;
			if ( command == "function" )
				command = prop.Value.Value<string>();

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

			reply.datagram.payload = JObject.FromObject( payload );

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

		public JObject	  payload { get; set; }
	}

	[System.SerializableAttribute()]
	public class HeaderType
	{
		public string	   specificationVersion { get; set; } = "1.3.0";

		public AddressType addressSource		{ get; set; }

		public AddressType addressDestination	{ get; set; }

		public ulong	   msgCounter			{ get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public ulong?	   msgCounterReference	{ get; set; }

		public string	   cmdClassifier		{ get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public bool?	   ackRequest			{ get; set; }
	}

	[System.SerializableAttribute()]
	public class AddressType
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string device  { get; set; }

		public int[]  entity  { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int?	  feature { get; set; }
	}
}
