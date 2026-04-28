using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.SPINE.Commands;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EEBUS.SHIP.Messages
{
	public class DataMessage : ShipDataMessage<DataMessage>
	{
		static DataMessage()
		{
			Register( new Class() );
		}

		public DataMessage()
		{
		}

		public DataMessage( JsonNode payload )
		{
			this.data.payload = payload;
		}

		public DataMessage( string protocolId, JsonNode payload )
		{
			this.data.header.protocolId = protocolId;
			this.data.payload			= payload;
		}

		private static DataMessage Create(AddressType source, AddressType destination, string cmdClassifier, SpineCmdPayloadBase? payload)
		{
            SpineDatagramPayload messagePayload = new SpineDatagramPayload();
            messagePayload.datagram.header.addressSource = source;
            messagePayload.datagram.header.addressDestination = destination;
            messagePayload.datagram.header.msgCounter = DataMessage.NextCount;
            messagePayload.datagram.header.cmdClassifier = cmdClassifier;

            messagePayload.datagram.payload = payload?.ToJsonNode();
            DataMessage message = new DataMessage();
            message.SetPayload(JsonSerializer.SerializeToNode(messagePayload) ?? throw new Exception("Failed to serialize data message"));
			return message;
        }

		public static DataMessage CreateRead(AddressType source, AddressType destination, SpineCmdPayloadBase? payload) => Create(source, destination, "read", payload);
        public static DataMessage CreateSubscription(AddressType source, AddressType destination, string serverFeatureType, string clientDeviceId, string serverDeviceId)
        {
			//Fixed address for subscription
            var messageSource = new AddressType()
            {
                device = clientDeviceId,
                entity = [0],
                feature = 0
            };

            //Fixed address for subscription
            var messageDestination = new AddressType()
            {
                device = serverDeviceId,
                entity = [0],
                feature = 0
            };

            NodeManagementSubscriptionRequestCall callPayload = new NodeManagementSubscriptionRequestCall();
            SubscriptionRequestType subscriptionRequest = callPayload.cmd[0].nodeManagementSubscriptionRequestCall.subscriptionRequest;
            subscriptionRequest.clientAddress = source;
            subscriptionRequest.serverAddress = destination;
            subscriptionRequest.serverFeatureType = serverFeatureType;

            return Create(messageSource, messageDestination, "call", callPayload);
        }

        public DataMessage( SpineDatagramPayload datagram )
		{
			this.data.payload = JsonSerializer.SerializeToNode( datagram );
		}

		public new class Class : ShipDataMessage<DataMessage>.Class
		{
			public override DataMessage Create(ReadOnlySpan<byte> data/*, Connection connection */)
			{
				DataMessage dm = template.FromJsonVirtual( data/*, connection*/ );
				return dm;
			}
		}

		public DataType		  data { get; set; } = new();
		
		static private object mutex = new();
		static private ulong  count = 1; 

		static public ulong NextCount
		{
			get
			{
				lock ( mutex )
				{
					return count++;
				}
			}
		}

		public void SetPayload( JsonNode payload )
		{
			this.data.payload = payload;
		}

		public override async Task<(Connection.EState, Connection.ESubState)> NextServerState( Connection connection )
		{
			if (connection.State == Connection.EState.WaitingForCloseConfirm)
			{
				Debug.WriteLine("Waiting for close confirm, skipping message evaluation");
				return (Connection.EState.WaitingForCloseConfirm, Connection.ESubState.None);
			}

			if ( connection.State == Connection.EState.Connected )
			{
				if ( this.data.payload is JsonObject payloadObj && payloadObj.ContainsKey( "datagram" ) )
				{
					SpineDatagramPayload payload	   = this.data.payload.Deserialize<SpineDatagramPayload>() ?? throw new Exception( "Failed to deserialize SpineDatagramPayload" );
					string?				 cmdClassifier = payload.datagram?.header?.cmdClassifier;

					await payload.EvaluateAsync( connection );

					if ( cmdClassifier == "reply" || cmdClassifier == "notify" || cmdClassifier == "result")
					{
						return (connection.State, connection.SubState);
					}
					
					SpineDatagramPayload? answer = await payload.CreateAnswerAsync( NextCount,  connection );

					if ( null != answer )
					{
						DataMessage reply = new DataMessage( answer );
						if ( null != reply )
						{
							connection.PushDataMessage(reply);
							//await reply.Send( connection.WebSocket ).ConfigureAwait( false );
							return (Connection.EState.Connected, Connection.ESubState.None);
						}
						else
						{
							GetType();
						}
					}
					else
					{
						GetType();
					}
				}
			}

			throw new Exception( "Was waiting for Data" );
		}
	}

	[System.SerializableAttribute()]
	public class DataType
	{
		public ShipHeaderType header	{ get; set; } = new();

		public JsonNode?	  payload	{ get; set; }

		[JsonPropertyName("extension")]
		public ExtensionType?  extension	{ get; set; }
	}

	/// <remarks/>
	[System.SerializableAttribute()]
	public class ShipHeaderType
	{
		public string protocolId { get; set; } = "ee1.0";
	}

	[System.SerializableAttribute()]
	public partial class ExtensionType
	{
		public string extensionId { get; set; }

		public byte[] binary	  { get; set; }

		public string @string	  { get; set; }
	}
}
