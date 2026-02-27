using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Data.DataStructures;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.Net.EEBUS.SPINE.Types;

namespace EEBUS.SPINE.Commands
{
	public class ElectricalConnectionCharacteristicListData : SpineCmdPayload<CmdElectricalConnectionCharacteristicListDataType>
	{
		static ElectricalConnectionCharacteristicListData()
		{
			Register( "electricalConnectionCharacteristicListData", new Class() );
		}

		//static public ulong counter = 1;

		public new class Class : SpineCmdPayload<CmdElectricalConnectionCharacteristicListDataType>.Class
		{
			public override SpineCmdPayloadBase CreateNotify( Connection connection )
			{
				ElectricalConnectionCharacteristicListData payload = new ElectricalConnectionCharacteristicListData();
				payload.cmd = [new()];
				payload.cmd[0].function = "electricalConnectionCharacteristicListData";
				payload.cmd[0].filter = [new()];
				payload.cmd[0].electricalConnectionCharacteristicListData = new();

                //connection.Local.FillData<ElectricalConnectionCharacteristicDataType>( eccs, connection );

                List<ElectricalConnectionCharacteristicDataStructure> structures = connection.Local.GetDataStructures<ElectricalConnectionCharacteristicDataStructure>();
				payload.cmd[0].electricalConnectionCharacteristicListData.electricalConnectionCharacteristicData = structures.Select(structure => structure.Data).ToArray();

				return payload;
			}

            public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync(DatagramType datagram, HeaderType header, Connection connection)
            {
				if (datagram.header.cmdClassifier != "read") return null;

                ElectricalConnectionCharacteristicListData payload = new ElectricalConnectionCharacteristicListData();
                payload.cmd[0].electricalConnectionCharacteristicListData = new();
                List<ElectricalConnectionCharacteristicDataStructure> structures = connection.Local.GetDataStructures<ElectricalConnectionCharacteristicDataStructure>();
                payload.cmd[0].electricalConnectionCharacteristicListData.electricalConnectionCharacteristicData = structures.Select(structure => structure.Data).ToArray();

                return payload;
            }

            public override async Task WriteDataAsync(LocalDevice localDevice, DeviceData deviceData)
            {
				bool didChange = false;

				if (deviceData.Lpc != null && !deviceData.Lpc.IsEmpty())
				{
                    var consumptionDataStructures = localDevice.GetDataStructures<ElectricalConnectionCharacteristicDataStructure>().Where(ds => ds.CharacteristicType == "contractualConsumptionNominalMax");
					foreach (var consumptionDataStructure in consumptionDataStructures)
					{
						didChange = deviceData.Lpc.ContractualNominalMax != null && consumptionDataStructure.Number != deviceData.Lpc.ContractualNominalMax;
						consumptionDataStructure.Number = deviceData.Lpc.ContractualNominalMax ?? consumptionDataStructure.Number;
					}
				}

                if (deviceData.Lpp != null && !deviceData.Lpp.IsEmpty())
                {
                    var productionDataStructures = localDevice.GetDataStructures<ElectricalConnectionCharacteristicDataStructure>().Where(ds => ds.CharacteristicType == "contractualProductionNominalMax");
                    foreach (var productionDataStructure in productionDataStructures)
                    {
                        didChange = deviceData.Lpp.ContractualNominalMax != null && productionDataStructure.Number != deviceData.Lpp.ContractualNominalMax;
                        productionDataStructure.Number = deviceData.Lpp.ContractualNominalMax ?? productionDataStructure.Number;
                    }
                }

				if (didChange)
				{
                    await SendNotifyAsync(localDevice, localDevice.GetFeatureAddress("ElectricalConnection", true));
                }
            }
		}
	}

	[System.SerializableAttribute()]
	public class CmdElectricalConnectionCharacteristicListDataType : CmdType
	{
		public string										  function									 { get; set; }
		public FilterType[]									  filter									 { get; set; }
		public ElectricalConnectionCharacteristicListDataType electricalConnectionCharacteristicListData { get; set; }
	}

	//[System.SerializableAttribute()]
	//public class FilterType
	//{
	//	public object cmdControl { get; set; } = new { partial = new { } };
	//}

	[System.SerializableAttribute()]
	public class ElectricalConnectionCharacteristicListDataType
	{
		public ElectricalConnectionCharacteristicDataType[] electricalConnectionCharacteristicData { get; set; }
	}

	[System.SerializableAttribute()]
	public class ElectricalConnectionCharacteristicDataType
	{
		public uint				electricalConnectionId { get; set; }
		public uint				parameterId			   { get; set; }
		public uint				characteristicId	   { get; set; }
		public string			characteristicContext  { get; set; }
		public string			characteristicType	   { get; set; }
		public ScaledNumberType	value				   { get; set; } = new();
		public string			unit				   { get; set; }
	}
}
