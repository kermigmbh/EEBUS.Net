using EEBUS.KeyValues;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Data.KeyValues;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.SHIP.Messages;
using EEBUS.UseCases.ControllableSystem;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Xml;

namespace EEBUS.SPINE.Commands
{
	public class DeviceConfigurationKeyValueListData : SpineCmdPayload<CmdDeviceConfigurationKeyValueListDataType>
	{
		static DeviceConfigurationKeyValueListData()
		{
			Register( "deviceConfigurationKeyValueListData", new Class() );
		}

		public new class Class : SpineCmdPayload<CmdDeviceConfigurationKeyValueListDataType>.Class
		{
			public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync( DatagramType datagram, HeaderType header, Connection connection )
			{
				if ( datagram.header.cmdClassifier == "read" )
				{
					DeviceConfigurationKeyValueListData	    payload = new DeviceConfigurationKeyValueListData();
					DeviceConfigurationKeyValueListDataType data	= payload.cmd[0].deviceConfigurationKeyValueListData;

					List<DeviceConfigurationKeyValueDataType> datas = new();
					foreach ( var keyValue in connection.Local.KeyValues )
						datas.Add( keyValue.Data );

					data.deviceConfigurationKeyValueData = datas.ToArray();

					return payload;
				}
				else if ( datagram.header.cmdClassifier == "write" )
				{
					return new ResultData();
				}
				else
				{
					return null;
				}
			}

			public override async ValueTask EvaluateAsync( Connection connection, DatagramType datagram )
			{
				if (datagram.header.cmdClassifier == "write")
				{

					DeviceConfigurationKeyValueListData? payload = datagram.payload == null
						? null
						: System.Text.Json.JsonSerializer.Deserialize<DeviceConfigurationKeyValueListData>(datagram.payload);

					foreach (var kvp in payload?.cmd[0].deviceConfigurationKeyValueListData.deviceConfigurationKeyValueData ?? []) {
                        KeyValue? keyValue = connection.Local.KeyValues.FirstOrDefault(kv => kv.Data.keyId == kvp.keyId);
                        if (null != keyValue)
                        {
                            keyValue.SetValue(kvp.value);
                            await keyValue.SendEventAsync(connection);
                        }
                    }
                    await SendNotifyAsync(connection.Local, datagram.header.addressDestination);
                }
                else if (datagram.header.cmdClassifier == "reply" || datagram.header.cmdClassifier == "notify")
				{
                    DeviceConfigurationKeyValueListData? payload = datagram.payload == null
                        ? null
                        : System.Text.Json.JsonSerializer.Deserialize<DeviceConfigurationKeyValueListData>(datagram.payload);

					if (payload == null || connection.Remote == null) return;

                    foreach (var kvp in payload.cmd[0].deviceConfigurationKeyValueListData.deviceConfigurationKeyValueData)
					{
                        RemoteKeyValue? existing = connection.Remote.KeyValues.FirstOrDefault(kv => kv is RemoteKeyValue rkv && rkv.KeyId == kvp.keyId) as RemoteKeyValue;
						if (existing != null)
						{
							existing.Update(null, kvp);
						} else
						{
							connection.Remote.KeyValues.Add(new RemoteKeyValue(connection.Remote, null, kvp));
						}
					}

					await SendDeviceConfigurationChangedEventAsync(connection);
                }
			}

			private async Task SendDeviceConfigurationChangedEventAsync(Connection connection)
			{
				if (connection.Remote == null) return;

				var deviceConfigEvents = connection.Local.GetUseCaseEvents<DeviceConfigurationEvents>();
				foreach (var ev in deviceConfigEvents)
				{
					await ev.RemoteDeviceConfigurationChangedAsync(connection);
				}
			}

            public override JsonNode? CreateNotifyPayload(LocalDevice localDevice)
            {
                DeviceConfigurationKeyValueListData payload = new DeviceConfigurationKeyValueListData();
                DeviceConfigurationKeyValueListDataType data = payload.cmd[0].deviceConfigurationKeyValueListData;

                List<DeviceConfigurationKeyValueDataType> datas = new();
                foreach (var keyValue in localDevice.KeyValues)
                    datas.Add(keyValue.Data);

                data.deviceConfigurationKeyValueData = datas.ToArray();

                return payload.ToJsonNode();
            }

            public override async Task WriteDataAsync(LocalDevice localDevice, DeviceData deviceData)
            {
				bool didChange = false;

                if (deviceData.Lpc?.FailSafeLimit != null)
				{
                    FailsafeConsumptionActivePowerLimitKeyValue? lpcFailsafeLimitKeyValue = localDevice.GetKeyValue<FailsafeConsumptionActivePowerLimitKeyValue>();
					if (lpcFailsafeLimitKeyValue != null)
					{
						lpcFailsafeLimitKeyValue.Value = deviceData.Lpc.FailSafeLimit.Value;
						didChange = true;
					}
                }

                if (deviceData.Lpp?.FailSafeLimit != null)
                {
                    FailsafeProductionActivePowerLimitKeyValue? lppFailsafeLimitKeyValue = localDevice.GetKeyValue<FailsafeProductionActivePowerLimitKeyValue>();
					if (lppFailsafeLimitKeyValue != null)
					{
						lppFailsafeLimitKeyValue.Value = deviceData.Lpp.FailSafeLimit.Value;
						didChange = true;
					}
                }

				if (deviceData.FailSafeLimitDuration != null)
				{
                    FailsafeDurationMinimumKeyValue? failsafeDurationKeyValue = localDevice.GetKeyValue<FailsafeDurationMinimumKeyValue>();
					if (failsafeDurationKeyValue != null)
					{
						failsafeDurationKeyValue.Duration = XmlConvert.ToString(deviceData.FailSafeLimitDuration.Value);
						didChange = true;
					}
                }

				if (didChange)
				{
					await SendNotifyAsync(localDevice, localDevice.GetFeatureAddress("DeviceConfiguration", true));
				}
            }

            //private void SendNotify(Connection connection, DatagramType datagram)
            //{
            //    SpineDatagramPayload notify = new SpineDatagramPayload();
            //    notify.datagram.header.addressSource = datagram.header.addressDestination;
            //    notify.datagram.header.addressDestination = datagram.header.addressSource;
            //    notify.datagram.header.msgCounter = DataMessage.NextCount;
            //    notify.datagram.header.cmdClassifier = "notify";

            //    DeviceConfigurationKeyValueListData payload = new DeviceConfigurationKeyValueListData();
            //    DeviceConfigurationKeyValueListDataType data = payload.cmd[0].deviceConfigurationKeyValueListData;

            //    List<DeviceConfigurationKeyValueDataType> datas = new();
            //    foreach (var keyValue in connection.Local.KeyValues)
            //        datas.Add(keyValue.Data);

            //    data.deviceConfigurationKeyValueData = datas.ToArray();


                 
            //    notify.datagram.payload = payload.ToJsonNode();

            //    DataMessage limitMessage = new DataMessage();
            //    limitMessage.SetPayload(System.Text.Json.JsonSerializer.SerializeToNode(notify));

            //    connection.PushDataMessage(limitMessage);
            //}
        }
	}

	[System.SerializableAttribute()]
	public class CmdDeviceConfigurationKeyValueListDataType : CmdType
	{

		public DeviceConfigurationKeyValueListDataType deviceConfigurationKeyValueListData { get; set; } = new();
	}

	[System.SerializableAttribute()]
	public class DeviceConfigurationKeyValueListDataType
	{
		public DeviceConfigurationKeyValueDataType[] deviceConfigurationKeyValueData { get; set; }
	}

	[System.SerializableAttribute()]
	public class DeviceConfigurationKeyValueDataType
	{
		public int		 keyId			   { get; set; }

		public ValueType value			   { get; set; } = new();

		public bool		 isValueChangeable { get; set; }
	}

	[System.SerializableAttribute()]
	public class ValueType
	{
		public ScaledNumberType?	scaledNumber { get; set; }
		
		public string?			duration	 { get; set; }
	}

	[System.SerializableAttribute()]
	public class ScaledNumberType
	{
		public long?  number	{ get; set; }
		
		public short? scale	{ get; set; }
	}
}
