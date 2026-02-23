using EEBUS.KeyValues;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.SHIP.Messages;
using EEBUS.UseCases;
using EEBUS.UseCases.ControllableSystem;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
					DeviceConfigurationKeyValueListData payload = new DeviceConfigurationKeyValueListData();
					DeviceConfigurationKeyValueListDataType data = payload.cmd[0].deviceConfigurationKeyValueListData;

					List<DeviceConfigurationKeyValueDataType> datas = new();
					foreach ( var keyValue in connection.Local.KeyValues )
						datas.Add( keyValue.Data );

					data.deviceConfigurationKeyValueData = datas.ToArray();

					return payload;
				}
				else if ( datagram.header.cmdClassifier == "write" )
				{
					// ApprovalResult was set in Evaluate (which runs before CreateAnswer)
					var approvalResult = datagram.ApprovalResult ?? WriteApprovalResult.Accept();
					return ResultData.FromApprovalResult( approvalResult );
				}
				else
				{
					return null;
				}
			}

			private WriteApprovalResult GetApprovalForKeyValue( Connection connection, KeyValue keyValue, ValueType value )
			{
				string? remoteDeviceId = connection.Remote?.DeviceId;
				string? remoteSki = connection.Remote?.SKI?.ToString();

				switch ( keyValue.KeyName )
				{
					case "failsafeConsumptionActivePowerLimit":
					{
						var request = new FailsafeLimitWriteRequest(
							PowerDirection.Consumption,
							value.scaledNumber?.number ?? 0,
							value.scaledNumber?.scale ?? 0,
							remoteDeviceId,
							remoteSki
						);

						var handlers = connection.Local.GetUseCaseEvents<LPCEvents>();
						if ( handlers.Count == 0 )
							return WriteApprovalResult.Accept( "No handlers - auto-approved" );

						foreach ( var handler in handlers )
						{
							var result = handler.ApproveFailsafeLimitWrite( request );
							if ( !result.Approved )
								return result;
						}
						return WriteApprovalResult.Accept();
					}

					case "failsafeProductionActivePowerLimit":
					{
						var request = new FailsafeLimitWriteRequest(
							PowerDirection.Production,
							value.scaledNumber?.number ?? 0,
							value.scaledNumber?.scale ?? 0,
							remoteDeviceId,
							remoteSki
						);

						var handlers = connection.Local.GetUseCaseEvents<LPPEvents>();
						if ( handlers.Count == 0 )
							return WriteApprovalResult.Accept( "No handlers - auto-approved" );

						foreach ( var handler in handlers )
						{
							var result = handler.ApproveFailsafeLimitWrite( request );
							if ( !result.Approved )
								return result;
						}
						return WriteApprovalResult.Accept();
					}

					case "failsafeDurationMinimum":
					{
						TimeSpan duration = TimeSpan.Zero;
						if ( !string.IsNullOrEmpty( value.duration ) )
						{
							try { duration = XmlConvert.ToTimeSpan( value.duration ); }
							catch { /* ignore */ }
						}

						var request = new FailsafeDurationWriteRequest( duration, remoteDeviceId, remoteSki );

						var handlers = connection.Local.GetUseCaseEvents<LPCorLPPEvents>();
						if ( handlers.Count == 0 )
							return WriteApprovalResult.Accept( "No handlers - auto-approved" );

						foreach ( var handler in handlers )
						{
							var result = handler.ApproveFailsafeDurationWrite( request );
							if ( !result.Approved )
								return result;
						}
						return WriteApprovalResult.Accept();
					}

					default:
						return WriteApprovalResult.Accept( "Unknown key - auto-approved" );
				}
			}

			public override async ValueTask EvaluateAsync( Connection connection, DatagramType datagram )
			{
				if ( datagram.header.cmdClassifier != "write" )
					return;

				DeviceConfigurationKeyValueListData? payload = datagram.payload == null
					? null
					: System.Text.Json.JsonSerializer.Deserialize<DeviceConfigurationKeyValueListData>(datagram.payload);

				if (payload == null)
					return;

				int keyId = payload.cmd[0].deviceConfigurationKeyValueListData.deviceConfigurationKeyValueData[0].keyId;
				ValueType value = payload.cmd[0].deviceConfigurationKeyValueListData.deviceConfigurationKeyValueData[0].value;

				KeyValue? keyValue = connection.Local.KeyValues.FirstOrDefault( kv => kv.Data.keyId == keyId );
				if ( keyValue == null )
				{
					datagram.ApprovalResult = WriteApprovalResult.Deny( "Unknown key ID" );
					return;
				}

				WriteApprovalResult approvalResult = GetApprovalForKeyValue( connection, keyValue, value );
				datagram.ApprovalResult = approvalResult;

				if ( approvalResult.Approved )
				{
					keyValue.SetValue( value );
					await keyValue.SendEventAsync( connection );
					await SendNotifyAsync(connection.Local, datagram.header.addressDestination);
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
		public int keyId { get; set; }

		public ValueType value { get; set; } = new();

		public bool isValueChangeable { get; set; }
	}

	[System.SerializableAttribute()]
	public class ValueType
	{
		public ScaledNumberType? scaledNumber { get; set; }

		public string? duration { get; set; }
	}

	[System.SerializableAttribute()]
	public class ScaledNumberType
	{
		public long? number { get; set; }

		public short? scale { get; set; }
	}
}