using EEBUS.Messages;
using System.Text.Json.Serialization;

namespace EEBUS.SPINE.Commands
{
    public class ElectricalConnectionParameterDescriptionListData : SpineCmdPayload<CmdElectricalConnectionParameterDescriptionListDataType>
    {
        static ElectricalConnectionParameterDescriptionListData()
        {
            Register("electricalConnectionParameterDescriptionListData", new Class());
        }

        static public ulong counter = 1;

        public new class Class : SpineCmdPayload<CmdElectricalConnectionParameterDescriptionListDataType>.Class
        {
            public override async ValueTask<SpineCmdPayloadBase> CreateAnswerAsync(DatagramType datagram, HeaderType header, Connection connection)
            {
                if (datagram.header.cmdClassifier == "read")
                {
                    ElectricalConnectionParameterDescriptionListData payload = new ElectricalConnectionParameterDescriptionListData();
                    payload.cmd = [new()];
                    payload.cmd[0].electricalConnectionParameterDescriptionListData = new();

                    List<ElectricalConnectionParameterDescriptionDataType> ecpdds = new();
                    connection.Local.FillData<ElectricalConnectionParameterDescriptionDataType>(ecpdds, connection);
                    payload.cmd[0].electricalConnectionParameterDescriptionListData.electricalConnectionParameterDescriptionData = ecpdds.ToArray();

                    return payload;
                }
                else if (datagram.header.cmdClassifier == "write")
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
        public ElectricalConnectionParameterDescriptionListDataType? electricalConnectionParameterDescriptionListData { get; set; }
    }

    [System.SerializableAttribute()]
    public class ElectricalConnectionParameterDescriptionListDataType
    {
        public ElectricalConnectionParameterDescriptionDataType[]? electricalConnectionParameterDescriptionData { get; set; }
    }

    [System.SerializableAttribute()]
    public class ElectricalConnectionParameterDescriptionDataType
    {
        public uint electricalConnectionId { get; set; }
        public uint measurementId { get; set; }
        public string voltageType { get; set; }

        // previously had [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? acMeasuredPhases { get; set; }

        public string? acMeasuredInReferenceTo { get; set; }

        public string? acMeasurementType { get; set; }

        public string? acMeasurementVariant { get; set; }
    }
}