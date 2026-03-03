using EEBUS.Messages;
using EEBUS.Net.EEBUS.Data.KeyValues;
using EEBUS.UseCases.ControllableSystem;
using System.Text.Json.Serialization;

namespace EEBUS.SPINE.Commands
{
	public class DeviceConfigurationKeyValueDescriptionListData : SpineCmdPayload<CmdDeviceConfigurationKeyValueDescriptionListDataType>
	{
		static DeviceConfigurationKeyValueDescriptionListData()
		{
			Register( "deviceConfigurationKeyValueDescriptionListData", new Class() );
		}

		public new class Class : SpineCmdPayload<CmdDeviceConfigurationKeyValueDescriptionListDataType>.Class
		{
			public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync( DatagramType datagram, HeaderType header, Connection connection )
			{
				DeviceConfigurationKeyValueDescriptionListData	   payload = new DeviceConfigurationKeyValueDescriptionListData();
				DeviceConfigurationKeyValueDescriptionListDataType data	   = payload.cmd[0].deviceConfigurationKeyValueDescriptionListData;

				List<DeviceConfigurationKeyValueDescriptionDataType> datas = new();
				foreach ( var keyValue in connection.Local.KeyValues )
					datas.Add( keyValue.DescriptionData );

				data.deviceConfigurationKeyValueDescriptionData = datas.ToArray();

				return payload;
			}

            public override async ValueTask EvaluateAsync(Connection connection, DatagramType datagram)
            {
                if (datagram.header.cmdClassifier == "reply" || datagram.header.cmdClassifier == "notify")
				{
                    DeviceConfigurationKeyValueDescriptionListData? payload = datagram.payload == null
                        ? null
                        : System.Text.Json.JsonSerializer.Deserialize<DeviceConfigurationKeyValueDescriptionListData>(datagram.payload);

                    if (payload == null || connection.Remote == null) return;

                    foreach (var kvp in payload.cmd[0].deviceConfigurationKeyValueDescriptionListData.deviceConfigurationKeyValueDescriptionData ?? [])
                    {
                        RemoteKeyValue? existing = connection.Remote.KeyValues.FirstOrDefault(kv => kv is RemoteKeyValue rkv && rkv.KeyId == kvp.keyId) as RemoteKeyValue;
                        if (existing != null)
                        {
                            existing.Update(kvp, null);
                        }
                        else
                        {
                            connection.Remote.KeyValues.Add(new RemoteKeyValue(connection.Remote, kvp, null));
                        }
                    }

                    await SendDeviceConfigurationChangedEventAsync(connection);
                }
            }

            private async Task SendDeviceConfigurationChangedEventAsync(Connection connection)
            {
                if (connection.Remote == null) return;

                var deviceConfigEvents = connection.Local.GetUseCaseEvents<MonitoringUseCaseEvents>();
                foreach (var ev in deviceConfigEvents)
                {
                    await ev.RemoteDeviceConfigurationChangedAsync(connection);
                }
            }
        }
	}

	[System.SerializableAttribute()]
	public class CmdDeviceConfigurationKeyValueDescriptionListDataType : CmdType
	{
		public DeviceConfigurationKeyValueDescriptionListDataType deviceConfigurationKeyValueDescriptionListData { get; set; } = new();
	}

	[System.SerializableAttribute()]
	public class DeviceConfigurationKeyValueDescriptionListDataType
	{
		public DeviceConfigurationKeyValueDescriptionDataType[]? deviceConfigurationKeyValueDescriptionData { get; set; }
	}

	[System.SerializableAttribute()]
	public class DeviceConfigurationKeyValueDescriptionDataType
	{
		public int	  keyId		{ get; set; }

		public string keyName	{ get; set; }

		public string valueType	{ get; set; }

		public string? unit		{ get; set; }
	}
}
