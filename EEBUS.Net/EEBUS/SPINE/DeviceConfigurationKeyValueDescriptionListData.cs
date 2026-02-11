using System.Text.Json.Serialization;

using EEBUS.Messages;

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
