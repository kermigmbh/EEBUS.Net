using EEBUS.KeyValues;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.SHIP.Messages;
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
				if ( datagram.header.cmdClassifier != "write" )
					return;

				DeviceConfigurationKeyValueListData? payload = datagram.payload == null
					? null
					: System.Text.Json.JsonSerializer.Deserialize<DeviceConfigurationKeyValueListData>(datagram.payload);

				int		  keyId = payload.cmd[0].deviceConfigurationKeyValueListData.deviceConfigurationKeyValueData[0].keyId;
				ValueType value = payload.cmd[0].deviceConfigurationKeyValueListData.deviceConfigurationKeyValueData[0].value;

				KeyValue? keyValue = connection.Local.KeyValues.FirstOrDefault( kv => kv.Data.keyId == keyId );
				if (null != keyValue)
				{
					keyValue.SetValue(value);
					await keyValue.SendEventAsync(connection);
					await SendNotifyAsync(connection.Local, datagram.header.addressDestination);
					//SendNotify(connection, datagram);
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
