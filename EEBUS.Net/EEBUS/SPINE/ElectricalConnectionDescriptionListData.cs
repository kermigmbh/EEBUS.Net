using EEBUS.Messages;
using Newtonsoft.Json;

namespace EEBUS.SPINE.Commands
{
	public class ElectricalConnectionDescriptionListData : SpineCmdPayload<CmdElectricalConnectionDescriptionListDataType>
	{
		static ElectricalConnectionDescriptionListData()
		{
			Register( "electricalConnectionDescriptionListData", new Class() );
		}

		static public ulong counter = 1;

		public new class Class : SpineCmdPayload<CmdElectricalConnectionDescriptionListDataType>.Class
		{
			public override SpineCmdPayloadBase CreateAnswer( DatagramType datagram, HeaderType header, Connection connection )
			{
				if ( datagram.header.cmdClassifier == "read" )
				{
					ElectricalConnectionDescriptionListData payload = new ElectricalConnectionDescriptionListData();
					payload.cmd = [new()];
					ElectricalConnectionDescriptionListDataType data = payload.cmd[0].electricalConnectionDescriptionListData = new();
					data.electricalConnectionDescriptionData = [new()];

					data.electricalConnectionDescriptionData[0].electricalConnectionId	= 0;    // ToDo: source??
					data.electricalConnectionDescriptionData[0].powerSupplyType			= "ac";
					data.electricalConnectionDescriptionData[0].acConnectedPhases		= 3;
					data.electricalConnectionDescriptionData[0].positiveEnergyDirection	= "consume";

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
	public class CmdElectricalConnectionDescriptionListDataType : CmdType
	{
		public ElectricalConnectionDescriptionListDataType electricalConnectionDescriptionListData { get; set; }
	}

	[System.SerializableAttribute()]
	public class ElectricalConnectionDescriptionListDataType
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public ElectricalConnectionDescriptionDataType[] electricalConnectionDescriptionData { get; set; }
	}

	[System.SerializableAttribute()]
	public class ElectricalConnectionDescriptionDataType
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public uint	  electricalConnectionId  { get; set; }
		
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string powerSupplyType		  { get; set; }
		
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public uint	  acConnectedPhases		  { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string positiveEnergyDirection { get; set; }
	}
}
