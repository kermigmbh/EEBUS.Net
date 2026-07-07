using EEBUS.Features;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Data.DataStructures;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.Net.EEBUS.SPINE.Types;
using EEBUS.SHIP.Messages;
using System.Text.Json.Nodes;

namespace EEBUS.SPINE.Commands
{
    public class ElectricalConnectionCharacteristicListData : SpineCmdPayload<CmdElectricalConnectionCharacteristicListDataType>
    {
        static ElectricalConnectionCharacteristicListData()
        {
            Register("electricalConnectionCharacteristicListData", new Class());
        }

        //static public ulong counter = 1;

        public new class Class : SpineCmdPayload<CmdElectricalConnectionCharacteristicListDataType>.Class
        {
            public override SpineCmdPayloadBase? CreateRead(Connection connection)
            {
                return new ElectricalConnectionCharacteristicListData();
            }

            public override SpineCmdPayloadBase CreateNotify(Connection connection)
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
                if (structures.Count > 0)
                {
                    payload.cmd[0].electricalConnectionCharacteristicListData.electricalConnectionCharacteristicData = structures.Select(structure => structure.Data).ToArray();
                }
                return payload;
            }

            public override JsonNode? CreateNotifyPayload(LocalDevice localDevice)
            {
                ElectricalConnectionCharacteristicListData payload = new ElectricalConnectionCharacteristicListData();
                payload.cmd[0].electricalConnectionCharacteristicListData = new();
                List<ElectricalConnectionCharacteristicDataStructure> structures = localDevice.GetDataStructures<ElectricalConnectionCharacteristicDataStructure>();
                payload.cmd[0].electricalConnectionCharacteristicListData.electricalConnectionCharacteristicData = structures.Select(s => s.Data).ToArray();
                return payload.ToJsonNode();
            }

            public override async ValueTask EvaluateAsync(Connection connection, DatagramType datagram)
            {
                if (datagram.header.cmdClassifier != "notify" && datagram.header.cmdClassifier != "reply")
                    return;

                ElectricalConnectionCharacteristicListData? command = datagram.payload == null
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<ElectricalConnectionCharacteristicListData>(datagram.payload);

                if (command == null || command.cmd == null || command.cmd.Length == 0 || connection.Remote == null)
                    return;

                List<ElectricalConnectionCharacteristicDataStructure> structures = connection.Remote.GetDataStructures<ElectricalConnectionCharacteristicDataStructure>();
                foreach (ElectricalConnectionCharacteristicDataType item in command.cmd.First().electricalConnectionCharacteristicListData.electricalConnectionCharacteristicData ?? [])
                {
                    var structure = structures.FirstOrDefault(s =>
                        s.Data.characteristicType == item.characteristicType &&
                        s.Data.electricalConnectionId == item.electricalConnectionId &&
                        s.Data.characteristicId == item.characteristicId &&
                        s.Data.parameterId == item.parameterId
                    );

                    if (structure == null)
                    {
                        structure = new ElectricalConnectionCharacteristicDataStructure(item.characteristicType, item.value.number, item.value.scale ?? 0);
                        connection.Remote.Add(structure);
                        structure.Id = item.characteristicId;
                        structure.ElectricalConnectionId = item.electricalConnectionId;
                        structure.ParameterId = item.parameterId;
                    } else
                    {
                        structure.Number = item.value.number;
                        structure.Scale = item.value.scale ?? 0;
                    }
                }
            }

            public override async Task WriteDataAsync(Connection connection, DeviceData deviceData)
            {

                LocalDevice localDevice = connection.Local;
                bool didChange = false;

                if (deviceData.Lpc != null && !deviceData.Lpc.IsEmpty())
                {
                    var consumptionDataStructures = localDevice.GetDataStructures<ElectricalConnectionCharacteristicDataStructure>().Where(ds => ds.CharacteristicType == "contractualConsumptionNominalMax");
                    foreach (var consumptionDataStructure in consumptionDataStructures)
                    {
                        didChange = didChange || (deviceData.Lpc.ContractualNominalMax != null && consumptionDataStructure.Number != deviceData.Lpc.ContractualNominalMax);
                        consumptionDataStructure.Number = deviceData.Lpc.ContractualNominalMax ?? consumptionDataStructure.Number;
                    }
                }

                if (deviceData.Lpp != null && !deviceData.Lpp.IsEmpty())
                {
                    var productionDataStructures = localDevice.GetDataStructures<ElectricalConnectionCharacteristicDataStructure>().Where(ds => ds.CharacteristicType == "contractualProductionNominalMax");
                    foreach (var productionDataStructure in productionDataStructures)
                    {
                        didChange = didChange || (deviceData.Lpp.ContractualNominalMax != null && productionDataStructure.Number != deviceData.Lpp.ContractualNominalMax);
                        productionDataStructure.Number = deviceData.Lpp.ContractualNominalMax ?? productionDataStructure.Number;
                    }
                }

                if (didChange)
                {
                    AddressType? featureAddress = localDevice.GetFeatureAddress("ElectricalConnection", true);
                    if (featureAddress != null)
                    {
                        await SendNotifyAsync(localDevice, featureAddress);
                    }
                }
            }

            public override async Task ReadDataAsync(Connection connection, DeviceData deviceData)
            {
                if (connection.Remote == null) return;

                AddressType? featureSourceAddress = connection.Local.GetFeatureAddress("ElectricalConnection", false);
                AddressType? featureDestinationAddress = connection.Remote.GetFeatureAddress("ElectricalConnection", true);

                if (featureSourceAddress == null || featureDestinationAddress == null) return;

                if (deviceData.Lpc != null || deviceData.Lpp != null)
                {
                    var electricalConnectionCharacteristicListData = await ReadFunctionFromRemoteAsync<ElectricalConnectionCharacteristicListData>(connection, "ElectricalConnection", "electricalConnectionCharacteristicListData");
                    if (electricalConnectionCharacteristicListData != null)
                    {
                        var contractualConsumptionNominalMax = electricalConnectionCharacteristicListData.cmd.First().electricalConnectionCharacteristicListData.electricalConnectionCharacteristicData?.FirstOrDefault(d => d.characteristicType == "contractualConsumptionNominalMax")?.value.ToLong();
                        var contractualProductionNominalMax = electricalConnectionCharacteristicListData.cmd.First().electricalConnectionCharacteristicListData.electricalConnectionCharacteristicData?.FirstOrDefault(d => d.characteristicType == "contractualProductionNominalMax")?.value.ToLong();

                        deviceData.Lpc?.ContractualNominalMax = contractualConsumptionNominalMax;
                        deviceData.Lpp?.ContractualNominalMax = contractualProductionNominalMax;
                    }
                }
            }
        }
    }

    [System.SerializableAttribute()]
    public class CmdElectricalConnectionCharacteristicListDataType : CmdType
    {
        public string function { get; set; } = "electricalConnectionCharacteristicListData";
        public FilterType[] filter { get; set; }
        public ElectricalConnectionCharacteristicListDataType electricalConnectionCharacteristicListData { get; set; } = new();
    }

    //[System.SerializableAttribute()]
    //public class FilterType
    //{
    //	public object cmdControl { get; set; } = new { partial = new { } };
    //}

    [System.SerializableAttribute()]
    public class ElectricalConnectionCharacteristicListDataType
    {
        public ElectricalConnectionCharacteristicDataType[]? electricalConnectionCharacteristicData { get; set; }
    }

    [System.SerializableAttribute()]
    public class ElectricalConnectionCharacteristicDataType
    {
        public uint electricalConnectionId { get; set; }
        public uint parameterId { get; set; }
        public uint characteristicId { get; set; }
        public string characteristicContext { get; set; }
        public string characteristicType { get; set; }
        public ScaledNumberType value { get; set; } = new();
        public string unit { get; set; }
    }
}
