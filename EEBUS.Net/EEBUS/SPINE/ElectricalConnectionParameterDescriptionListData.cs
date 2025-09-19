using EEBUS.Messages;
using Newtonsoft.Json;

namespace EEBUS.SPINE.Commands
{
	public class ElectricalConnectionParameterDescriptionListData : SpineCmdPayload<CmdElectricalConnectionParameterDescriptionListDataType>
	{
		static ElectricalConnectionParameterDescriptionListData()
		{
			Register( "electricalConnectionParameterDescriptionListData", new Class() );
		}

		static public ulong counter = 1;

		public new class Class : SpineCmdPayload<CmdElectricalConnectionParameterDescriptionListDataType>.Class
		{
			public override SpineCmdPayloadBase CreateAnswer( DatagramType datagram, HeaderType header, Connection connection )
			{
				if ( datagram.header.cmdClassifier == "read" )
				{
					ElectricalConnectionParameterDescriptionListData payload = new ElectricalConnectionParameterDescriptionListData();
					payload.cmd = [new()];
					payload.cmd[0].electricalConnectionParameterDescriptionListData = new();

					List<ElectricalConnectionParameterDescriptionDataType> ecpdds = new();
					connection.Local.FillData<ElectricalConnectionParameterDescriptionDataType>( ecpdds, connection );
					payload.cmd[0].electricalConnectionParameterDescriptionListData.electricalConnectionParameterDescriptionData = ecpdds.ToArray();

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
	public class CmdElectricalConnectionParameterDescriptionListDataType : CmdType
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public ElectricalConnectionParameterDescriptionListDataType electricalConnectionParameterDescriptionListData { get; set; }
	}

	[System.SerializableAttribute()]
	public class ElectricalConnectionParameterDescriptionListDataType
	{
		public ElectricalConnectionParameterDescriptionDataType[] electricalConnectionParameterDescriptionData { get; set; }
	}

	[System.SerializableAttribute()]
	public class ElectricalConnectionParameterDescriptionDataType
	{
		public uint	  electricalConnectionId  { get; set; }
		public uint	  measurementId			  { get; set; }
		public string voltageType			  { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string acMeasuredPhases		  { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string acMeasuredInReferenceTo { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string acMeasurementType		  { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string acMeasurementVariant	  { get; set; }
	}
}
