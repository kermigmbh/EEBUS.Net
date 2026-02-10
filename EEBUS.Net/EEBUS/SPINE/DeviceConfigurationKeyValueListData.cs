using System.Text.Json.Serialization;

using EEBUS.Messages;
using EEBUS.Models;

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
				}
			}
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
