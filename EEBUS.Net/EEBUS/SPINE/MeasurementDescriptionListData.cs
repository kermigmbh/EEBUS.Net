using EEBUS.Features;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net;
using EEBUS.UseCases.ControllableSystem;
using System.Text.Json.Serialization;

namespace EEBUS.SPINE.Commands
{
	public class MeasurementDescriptionListData : SpineCmdPayload<CmdMeasurementDescriptionListDataType>
	{
		static MeasurementDescriptionListData()
		{
			Register( "measurementDescriptionListData", new Class() );
		}

		static public ulong counter = 1;

		public new class Class : SpineCmdPayload<CmdMeasurementDescriptionListDataType>.Class
		{
            public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync(DatagramType datagram, HeaderType header, Connection connection)
            {
                if (datagram.header.cmdClassifier == "read")
                {
                    AddressType? address = connection.Local.GetFeatureAddress("Measurement", true);
                    if (address == null) return null;

                    Entity? entity = connection.Local.Entities.FirstOrDefault(e => e.Index.SequenceEqual(address.entity));
                    MeasurementServerFeature? measurementFeature = entity?.Features.FirstOrDefault(f => f.Index == address.feature) as MeasurementServerFeature;
                    if (measurementFeature == null) return null;

                    MeasurementDescriptionListData measurementDescriptionListData = new();
                    List<MeasurementDescriptionDataType> measurementData = new();
                    foreach (var data in measurementFeature.measurementData)
                    {
                        if (data.measurementDescriptionDataType != null)
                        {
                            measurementData.Add(data.measurementDescriptionDataType);
                        }
                    }

                    if (measurementData.Count > 0)
                    {
                        measurementDescriptionListData.cmd[0].measurementDescriptionListData.measurementDescriptionData = measurementData.ToArray();
                    }
                    return measurementDescriptionListData;
                } else
                {
                    return null;
                }
            }

            public override SpineCmdPayloadBase? CreateRead(Connection connection)
            {
                return new MeasurementDescriptionListData();
            }

            public override async ValueTask EvaluateAsync(Connection connection, DatagramType datagram)
            {
				if (datagram.header.cmdClassifier == "reply" || datagram.header.cmdClassifier == "notify")
				{
					MeasurementDescriptionListData? command = datagram.payload == null
						? null
						: JsonHelper.FromJsonNode<MeasurementDescriptionListData>(datagram.payload);
					if (command == null || command.cmd == null || command.cmd.Length == 0)
						return;

					Entity? entity = connection.Remote?.Entities.FirstOrDefault(e => e.Index.SequenceEqual(datagram.header.addressSource.entity));
					MeasurementServerFeature? measurementFeature = entity?.Features.FirstOrDefault(f => f.Index == datagram.header.addressSource.feature) as MeasurementServerFeature;
					if (measurementFeature == null) return;

					foreach (MeasurementDescriptionDataType measurementDescription in command.cmd.First().measurementDescriptionListData.measurementDescriptionData ?? [])
					{
						MeasurementData.MeasurementData? corresponding = measurementFeature.measurementData.FirstOrDefault(data => data.measurementId == measurementDescription.measurementId);
						if (corresponding == null)
						{
							measurementFeature.measurementData.Add(new MeasurementData.MeasurementData
							{
								measurementId = measurementDescription.measurementId,
								measurementDescriptionDataType = measurementDescription
							});
						}
						else
						{
							corresponding.measurementDescriptionDataType = measurementDescription;
						}
					}
					await SendMeasurementDataChangedAsync(connection, measurementFeature.measurementData);
				}
            }

            private async Task SendMeasurementDataChangedAsync(Connection connection, List<MeasurementData.MeasurementData> measurementData)
            {
                if (connection.Remote == null) return;

                var deviceConfigEvents = connection.Local.GetUseCaseEvents<MonitoringUseCaseEvents>();
                foreach (var ev in deviceConfigEvents)
                {
                    await ev.DataUpdateMeasurementsAsync(measurementData, connection.Remote.SKI.ToString());
                }
            }
        }
	}

	[System.SerializableAttribute()]
	public class CmdMeasurementDescriptionListDataType : CmdType
	{
        [JsonPropertyName("measurementDescriptionListData")]
        public MeasurementDescriptionListDataType measurementDescriptionListData { get; set; } = new();
	}

	[System.SerializableAttribute()]
	public class MeasurementDescriptionListDataType
	{
        public MeasurementDescriptionDataType[]? measurementDescriptionData { get; set; }
	}

	[System.SerializableAttribute()]
	public class MeasurementDescriptionDataType
	{
		public uint	  measurementId	  { get; set; }
		public string measurementType { get; set; }
		public string commodityType	  { get; set; }
		public string unit			  { get; set; }
		public string scopeType		  { get; set; }
	}
}
