using EEBUS.Messages;
using Newtonsoft.Json;

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
			public override SpineCmdPayloadBase CreateAnswer( DatagramType datagram, HeaderType header, Connection connection )
			{
				if ( datagram.header.cmdClassifier == "read" )
				{
					MeasurementDescriptionListData payload = new MeasurementDescriptionListData();
					payload.cmd = [new()];
					payload.cmd[0].measurementDescriptionListData = new();

					List<MeasurementDescriptionDataType> mddts = new();
					connection.Local.FillData<MeasurementDescriptionDataType>( mddts, connection );
					payload.cmd[0].measurementDescriptionListData.measurementDescriptionData = mddts.ToArray();

					//data.measurementDescriptionData = [new(), new(), new(), new(), new(), new(), new(), new(), new(), new()];
					//data.measurementDescriptionData[0].measurementId   = 0;
					//data.measurementDescriptionData[0].measurementType = "power";
					//data.measurementDescriptionData[0].commodityType   = "electricity";
					//data.measurementDescriptionData[0].unit			   = "W";
					//data.measurementDescriptionData[0].scopeType	   = "acPowerTotal";

					//data.measurementDescriptionData[1].measurementId   = 1;
					//data.measurementDescriptionData[1].measurementType = "energy";
					//data.measurementDescriptionData[1].commodityType   = "electricity";
					//data.measurementDescriptionData[1].unit			   = "Wh";
					//data.measurementDescriptionData[1].scopeType	   = "gridFeedIn";

					//data.measurementDescriptionData[2].measurementId   = 2;
					//data.measurementDescriptionData[2].measurementType = "energy";
					//data.measurementDescriptionData[2].commodityType   = "electricity";
					//data.measurementDescriptionData[2].unit			   = "Wh";
					//data.measurementDescriptionData[2].scopeType	   = "gridConsumption";

					//data.measurementDescriptionData[3].measurementId   = 3;
					//data.measurementDescriptionData[3].measurementType = "current";
					//data.measurementDescriptionData[3].commodityType   = "electricity";
					//data.measurementDescriptionData[3].unit			   = "A";
					//data.measurementDescriptionData[3].scopeType	   = "acCurrent";

					//data.measurementDescriptionData[4].measurementId   = 4;
					//data.measurementDescriptionData[4].measurementType = "current";
					//data.measurementDescriptionData[4].commodityType   = "electricity";
					//data.measurementDescriptionData[4].unit			   = "A";
					//data.measurementDescriptionData[4].scopeType	   = "acCurrent";

					//data.measurementDescriptionData[5].measurementId   = 5;
					//data.measurementDescriptionData[5].measurementType = "current";
					//data.measurementDescriptionData[5].commodityType   = "electricity";
					//data.measurementDescriptionData[5].unit			   = "A";
					//data.measurementDescriptionData[5].scopeType	   = "acCurrent";

					//data.measurementDescriptionData[6].measurementId   = 6;
					//data.measurementDescriptionData[6].measurementType = "voltage";
					//data.measurementDescriptionData[6].commodityType   = "electricity";
					//data.measurementDescriptionData[6].unit			   = "V";
					//data.measurementDescriptionData[6].scopeType	   = "acVoltage";

					//data.measurementDescriptionData[7].measurementId   = 7;
					//data.measurementDescriptionData[7].measurementType = "voltage";
					//data.measurementDescriptionData[7].commodityType   = "electricity";
					//data.measurementDescriptionData[7].unit			   = "V";
					//data.measurementDescriptionData[7].scopeType	   = "acVoltage";

					//data.measurementDescriptionData[8].measurementId   = 8;
					//data.measurementDescriptionData[8].measurementType = "voltage";
					//data.measurementDescriptionData[8].commodityType   = "electricity";
					//data.measurementDescriptionData[8].unit			   = "V";
					//data.measurementDescriptionData[8].scopeType	   = "acVoltage";

					//data.measurementDescriptionData[9].measurementId   = 9;
					//data.measurementDescriptionData[9].measurementType = "frequency";
					//data.measurementDescriptionData[9].commodityType   = "electricity";
					//data.measurementDescriptionData[9].unit			   = "Hz";
					//data.measurementDescriptionData[9].scopeType	   = "acFrequency";

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
		}
	}

	[System.SerializableAttribute()]
	public class CmdMeasurementDescriptionListDataType : CmdType
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public MeasurementDescriptionListDataType measurementDescriptionListData { get; set; }
	}

	[System.SerializableAttribute()]
	public class MeasurementDescriptionListDataType
	{
		public MeasurementDescriptionDataType[] measurementDescriptionData { get; set; }
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
