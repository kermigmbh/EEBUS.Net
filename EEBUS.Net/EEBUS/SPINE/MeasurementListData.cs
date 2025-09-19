using EEBUS.Messages;
using Newtonsoft.Json;

namespace EEBUS.SPINE.Commands
{
	public class MeasurementListData : SpineCmdPayload<CmdMeasurementListDataType>
	{
		static MeasurementListData()
		{
			Register( "measurementListData", new Class() );
		}

		static public ulong counter = 1;

		public new class Class : SpineCmdPayload<CmdMeasurementListDataType>.Class
		{
			public override SpineCmdPayloadBase CreateAnswer( DatagramType datagram, HeaderType header, Connection connection )
			{
				if ( datagram.header.cmdClassifier == "read" )
				{
					MeasurementListData payload = new MeasurementListData();
					payload.cmd = [new()];
					payload.cmd[0].measurementListData = new();

					List<MeasurementDataType> mdts = new();
					connection.Local.FillData<MeasurementDataType>( mdts, connection );
					payload.cmd[0].measurementListData.measurementData = mdts.ToArray();

					//MeasurementListDataType data = payload.cmd[0].measurementListData = new();
					//data.measurementData = [new(), new(), new(), new(), new(), new(), new(), new(), new(), new()];
					//data.measurementData[0].measurementId = 0;
					//data.measurementData[0].valueType	  = "value";
					//data.measurementData[0].value		  = new() { number = 3000, scale = 0 };
					//data.measurementData[0].valueSource	  = "measuredValue";

					//data.measurementData[1].measurementId = 1;
					//data.measurementData[1].valueType	  = "value";
					//data.measurementData[1].value		  = new() { number = -1000, scale = 0 };
					//data.measurementData[1].valueSource	  = "measuredValue";

					//data.measurementData[2].measurementId = 2;
					//data.measurementData[2].valueType	  = "value";
					//data.measurementData[2].value		  = new() { number = 12348, scale = 0 };
					//data.measurementData[2].valueSource	  = "measuredValue";

					//data.measurementData[3].measurementId = 3;
					//data.measurementData[3].valueType	  = "value";
					//data.measurementData[3].value		  = new() { number = 10, scale = 0 };
					//data.measurementData[3].valueSource	  = "measuredValue";

					//data.measurementData[4].measurementId = 4;
					//data.measurementData[4].valueType	  = "value";
					//data.measurementData[4].value		  = new() { number = 20, scale = 0 };
					//data.measurementData[4].valueSource	  = "measuredValue";

					//data.measurementData[5].measurementId = 5;
					//data.measurementData[5].valueType	  = "value";
					//data.measurementData[5].value		  = new() { number = 30, scale = 0 };
					//data.measurementData[5].valueSource	  = "measuredValue";

					//data.measurementData[6].measurementId = 6;
					//data.measurementData[6].valueType	  = "value";
					//data.measurementData[6].value		  = new() { number = 229, scale = 0 };
					//data.measurementData[6].valueSource	  = "measuredValue";

					//data.measurementData[7].measurementId = 7;
					//data.measurementData[7].valueType	  = "value";
					//data.measurementData[7].value		  = new() { number = 230, scale = 0 };
					//data.measurementData[7].valueSource	  = "measuredValue";

					//data.measurementData[8].measurementId = 8;
					//data.measurementData[8].valueType	  = "value";
					//data.measurementData[8].value		  = new() { number = 231, scale = 0 };
					//data.measurementData[8].valueSource	  = "measuredValue";

					//data.measurementData[9].measurementId = 9;
					//data.measurementData[9].valueType	  = "value";
					//data.measurementData[9].value		  = new() { number = 50, scale = 0 };
					//data.measurementData[9].valueSource	  = "measuredValue";

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

			public override SpineCmdPayloadBase CreateNotify( Connection connection )
			{
				MeasurementListData payload = new MeasurementListData();
				payload.cmd = [new()];
				payload.cmd[0].function			   = "measurementListData";
				payload.cmd[0].filter			   = [new()];
				payload.cmd[0].measurementListData = new();

				List<MeasurementDataType> mdts = new();
				connection.Local.FillData<MeasurementDataType>( mdts, connection );
				payload.cmd[0].measurementListData.measurementData = mdts.ToArray();

				return payload;
			}
		}
	}

	[System.SerializableAttribute()]
	public class CmdMeasurementListDataType : CmdType
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string				   function			   { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public FilterType[]			   filter			   { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public MeasurementListDataType measurementListData { get; set; }
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

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string			timestamp		 { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public ScaledNumberType	value			 { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public TimePeriodType	evaluationPeriod { get; set; }

		public string			valueSource		 { get; set; }
	}
}
