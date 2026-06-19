using EEBUS.Models;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.SHIP.Messages;
using EEBUS.UseCases.ControllableSystem;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EEBUS.Messages
{
	public abstract class SpineCmdPayloadBase
	{
		public SpineCmdPayloadBase()
		{
		}
		public abstract JsonNode? ToJsonNode();



        public abstract class Class
		{
            public abstract SpineCmdPayloadBase? FromJsonNode(JsonNode? node);

            public virtual async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync( DatagramType datagram, HeaderType header, Connection connection )
			{
				return null;
			}

			public virtual SpineCmdPayloadBase? CreateNotify( Connection connection )
			{
				return null;
			}

			public virtual SpineCmdPayloadBase? CreateRead( Connection connection )
			{
				return null;
			}
            public virtual SpineCmdPayloadBase? CreateWrite(Connection connection)
            {
                return null;
            }

            public virtual SpineCmdPayloadBase? CreateCall( Connection connection )
			{
				return null;
			}

			/// <summary>
			/// write to local device
			/// </summary>
			/// <param name="localDevice"></param>
			/// <param name="deviceData"></param>
			/// <returns></returns>
			public virtual Task WriteDataAsync(Connection connection, DeviceData deviceData)
			{
				return Task.CompletedTask;
			}


		

            public virtual JsonNode? CreateNotifyPayload(LocalDevice localDevice)
			{
				return null;
			}

            public virtual JsonNode? CreateWritePayload(LocalDevice localDevice)
            {
                return null;
            }

            public async Task SendNotifyAsync(LocalDevice localDevice, AddressType localAddress)
			{
				var payload = CreateNotifyPayload(localDevice);
                List<NotifyEvents> notifyEvents = localDevice.GetUseCaseEvents<NotifyEvents>();
                foreach (var ev in notifyEvents)
                {
                    await ev.NotifyAsync(payload, localAddress);
                }
            }

            protected async Task SendWriteAsync(Connection connection, AddressType localAddress, AddressType remoteAddress)
            {
				 

                SpineDatagramPayload reply = new SpineDatagramPayload();
                reply.datagram.header.addressSource = localAddress;
                reply.datagram.header.addressDestination = remoteAddress;
                reply.datagram.header.msgCounter = DataMessage.NextCount;
                reply.datagram.header.cmdClassifier = "write";

                reply.datagram.payload = CreateWritePayload(connection.Local); ;
                DataMessage dataMessage = new DataMessage();
                dataMessage.SetPayload(JsonSerializer.SerializeToNode(reply) ?? throw new Exception("Failed to serialize data message"));
                connection.PushDataMessage(dataMessage);

            }

            protected async Task SendConnectionStatusUpdatedEvent(Connection connection)
            {
                List<DeviceConnectionStatusEvents> notifyEvents = connection.Local.GetUseCaseEvents<DeviceConnectionStatusEvents>();
                foreach (var ev in notifyEvents)
                {
                    await ev.DeviceConnectionStatusUpdatedAsync(connection);
                }
            }

            public virtual async ValueTask EvaluateAsync( Connection connection, DatagramType datagram )
			{
			}

			public virtual bool? GetAnswerAckRequest()
			{
				return null;
			}
		}

		static protected Dictionary<string, Class> commands = new Dictionary<string, Class>();

		static protected void Register( string cmd, Class cls )
		{
			commands.Add( cmd, cls );
		}

		static public Class? GetClass( string cmd )
		{
			if ( commands.TryGetValue( cmd, out Class? cls ) )
				return cls;

			return null;
		}
	}
}
