using System.Text.Json.Serialization;

using EEBUS.DataStructures;
using EEBUS.Messages;

namespace EEBUS.SPINE.Commands
{
	public class LoadControlLimitDescriptionListData : SpineCmdPayload<CmdLoadControlLimitDescriptionListDataType>
	{
		static LoadControlLimitDescriptionListData()
		{
			Register( "loadControlLimitDescriptionListData", new Class() );
		}

		public new class Class : SpineCmdPayload<CmdLoadControlLimitDescriptionListDataType>.Class
		{
			public override SpineCmdPayloadBase CreateAnswer( DatagramType datagram, HeaderType header, Connection connection )
			{
				LoadControlLimitDescriptionListData	    payload = new LoadControlLimitDescriptionListData();
				LoadControlLimitDescriptionListDataType data	= payload.cmd[0].loadControlLimitDescriptionListData;

				List<LoadControlLimitDescriptionDataType> datas = new();
				foreach ( LoadControlLimitDataStructure description in connection.Local.GetDataStructures<LoadControlLimitDataStructure>() )
					datas.Add( description.DescriptionData );

				data.loadControlLimitDescriptionData = datas.ToArray();

				return payload;
			}
		}
	}

	[System.SerializableAttribute()]
	public class CmdLoadControlLimitDescriptionListDataType : CmdType
	{
		public LoadControlLimitDescriptionListDataType loadControlLimitDescriptionListData { get; set; } = new();
	}

	[System.SerializableAttribute()]
	public class LoadControlLimitDescriptionListDataType
	{
		public LoadControlLimitDescriptionDataType[] loadControlLimitDescriptionData { get; set; }
	}

	[System.SerializableAttribute()]
	public class LoadControlLimitDescriptionDataType
	{
		public uint	  limitId		 { get; set; }

		public string limitType		 { get; set; }

		public string limitCategory	 { get; set; }

		public string limitDirection { get; set; }

		public uint	  measurementId	 { get; set; }
		
		public string unit			 { get; set; }
		
		public string? scopeType		 { get; set; }
		
		public string? label			 { get; set; }
	}
}
