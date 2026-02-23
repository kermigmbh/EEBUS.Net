using EEBUS.Models;
using EEBUS.Net.EEBUS.Models.Data;
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

			public virtual SpineCmdPayloadBase? CreateCall( Connection connection )
			{
				return null;
			}

			public virtual Task WriteDataAsync(LocalDevice localDevice, DeviceData deviceData)
			{
				return Task.CompletedTask;
			}

			public virtual JsonNode? CreateNotifyPayload(LocalDevice localDevice)
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

			protected async Task SendDiscoveryCompletedEvent(LocalDevice localDevice, RemoteDevice remoteDevice)
			{
                List<DeviceConnectionStatusEvents> notifyEvents = localDevice.GetUseCaseEvents<DeviceConnectionStatusEvents>();
                foreach (var ev in notifyEvents)
                {
					await ev.RemoteDiscoveryCompletedAsync(remoteDevice);
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
