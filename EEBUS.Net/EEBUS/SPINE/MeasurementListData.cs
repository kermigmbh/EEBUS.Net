using EEBUS.Features;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.Net.EEBUS.SPINE.Types;
using EEBUS.Net.Extensions;
using EEBUS.UseCases.ControllableSystem;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EEBUS.SPINE.Commands
{
	public class MeasurementListData : SpineCmdPayload<CmdMeasurementListDataType>
	{
		static MeasurementListData()
		{
			Register( "measurementListData", new Class() );
		}

		public new class Class : SpineCmdPayload<CmdMeasurementListDataType>.Class
		{
            public override SpineCmdPayloadBase? CreateRead(Connection connection)
            {
				return new MeasurementListData();
            }

            public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync(DatagramType datagram, HeaderType header, Connection connection)
            {
                AddressType? address = connection.Local.GetFeatureAddress("Measurement", true);
                if (address == null) return null;

                Entity? entity = connection.Local.Entities.FirstOrDefault(e => e.Index.SequenceEqual(address.entity));
                MeasurementServerFeature? measurementFeature = entity?.Features.FirstOrDefault(f => f.Index == address.feature) as MeasurementServerFeature;
                if (measurementFeature == null) return null;

                MeasurementListData measurementListData = new MeasurementListData();
                measurementListData.cmd[0].measurementListData.measurementData = measurementFeature.measurementData.Select(data => data.measurementDataType).ToArray();
				return measurementListData;
            }

            public override async ValueTask EvaluateAsync(Connection connection, DatagramType datagram)
            {
				if (datagram.header.cmdClassifier == "reply" || datagram.header.cmdClassifier == "notify")
				{

					MeasurementListData? command = datagram.payload == null
						? null
						: System.Text.Json.JsonSerializer.Deserialize<MeasurementListData>(datagram.payload);
					if (command == null || command.cmd == null || command.cmd.Length == 0)
						return;

					Entity? entity = connection.Remote?.Entities.FirstOrDefault(e => e.Index.SequenceEqual(datagram.header.addressSource.entity));
					MeasurementServerFeature? measurementFeature = entity?.Features.FirstOrDefault(f => f.Index == datagram.header.addressSource.feature) as MeasurementServerFeature;
					if (measurementFeature == null) return;

					foreach (MeasurementDataType measurement in command.cmd.First().measurementListData.measurementData)
					{
						MeasurementData.MeasurementData? corresponding = measurementFeature.measurementData.FirstOrDefault(data => data.measurementId == measurement.measurementId);
						if (corresponding == null)
						{
							measurementFeature.measurementData.Add(new MeasurementData.MeasurementData
							{
								measurementId = measurement.measurementId,
								measurementDataType = measurement
							});
						}
						else
						{
							corresponding.measurementDataType = measurement;
						}
					}
					await SendRemoteMeasurementDataChangedAsync(connection, measurementFeature.measurementData);
				}
            }

            private async Task SendRemoteMeasurementDataChangedAsync(Connection connection, List<MeasurementData.MeasurementData> measurementData)
            {
                if (connection.Remote == null) return;

                var deviceConfigEvents = connection.Local.GetUseCaseEvents<MonitoringUseCaseEvents>();
                foreach (var ev in deviceConfigEvents)
                {
                    await ev.DataUpdateMeasurementsAsync(measurementData, connection.Remote.SKI.ToString());
                }
            }

            public override Task WriteDataAsync(LocalDevice localDevice, DeviceData deviceData)
            {
				if (deviceData.Measurements == null) return Task.CompletedTask;

                AddressType? address = localDevice.GetFeatureAddress("Measurement", true);
				if (address == null) return Task.CompletedTask;

                Entity? entity = localDevice.Entities.FirstOrDefault(e => e.Index.SequenceEqual(address.entity));
                MeasurementServerFeature? measurementFeature = entity?.Features.FirstOrDefault(f => f.Index == address.feature) as MeasurementServerFeature;
				if (measurementFeature == null) return Task.CompletedTask;

				measurementFeature.measurementData.Update(deviceData.Measurements);
                return SendNotifyAsync(localDevice, address);
            }

            public override JsonNode? CreateNotifyPayload(LocalDevice localDevice)
            {
                AddressType? address = localDevice.GetFeatureAddress("Measurement", true);
                if (address == null) return null;

                Entity? entity = localDevice.Entities.FirstOrDefault(e => e.Index.SequenceEqual(address.entity));
                MeasurementServerFeature? measurementFeature = entity?.Features.FirstOrDefault(f => f.Index == address.feature) as MeasurementServerFeature;
				if (measurementFeature == null) return null;

				MeasurementListData measurementListData = new MeasurementListData();
				measurementListData.cmd[0].measurementListData.measurementData = measurementFeature.measurementData.Select(data => data.measurementDataType).ToArray();
				return measurementListData.ToJsonNode();
            }
        }
	}

	[System.SerializableAttribute()]
	public class CmdMeasurementListDataType : CmdType
	{
		[JsonPropertyName("function")]
		public string				   function			   { get; set; }

		[JsonPropertyName("filter")]
		public FilterType[]			   filter			   { get; set; }

		[JsonPropertyName("measurementListData")]
		public MeasurementListDataType measurementListData { get; set; } = new();
	}

	[System.SerializableAttribute()]
	public class MeasurementListDataType
	{
		public MeasurementDataType[] measurementData { get; set; }
	}

	[System.SerializableAttribute()]
	public class MeasurementDataType
	{
		public uint				measurementId	 { get; set; }

		public string			valueType		 { get; set; }

		[JsonPropertyName("timestamp")]
		public string?			timestamp		 { get; set; }

		[JsonPropertyName("value")]
		public ScaledNumberType? value			 { get; set; }

		[JsonPropertyName("evaluationPeriod")]
		public TimePeriodType? evaluationPeriod { get; set; }

		public string			valueSource		 { get; set; }
	}
}
