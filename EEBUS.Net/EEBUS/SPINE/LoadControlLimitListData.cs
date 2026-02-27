using System.Text.Json.Serialization;
using System.Xml;

using EEBUS.DataStructures;
using EEBUS.Messages;
using EEBUS.SHIP.Messages;
using EEBUS.UseCases;
using EEBUS.UseCases.ControllableSystem;
using System.Text.Json.Nodes;
using System.Text.Json;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.Net.EEBUS.SPINE.Types;

namespace EEBUS.SPINE.Commands
{
    public class LoadControlLimitListData : SpineCmdPayload<CmdLoadControlLimitListDataType>
    {
        static LoadControlLimitListData()
        {
            Register("loadControlLimitListData", new Class());
        }

        public new class Class : SpineCmdPayload<CmdLoadControlLimitListDataType>.Class
        {
            public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync(DatagramType datagram, HeaderType header, Connection connection)
            {
                if (datagram.header.cmdClassifier == "read")
                {
                    LoadControlLimitListData payload = new LoadControlLimitListData();
                    LoadControlLimitListDataType data = payload.cmd[0].loadControlLimitListData;
                    List<LoadControlLimitDataType> datas = new();

                    foreach (LoadControlLimitDataStructure structure in connection.Local.GetDataStructures<LoadControlLimitDataStructure>())
                    {
                        datas.Add(structure.Data);
                    }

                    data.loadControlLimitData = datas.ToArray();
                    return payload;
                }
                else if (datagram.header.cmdClassifier == "write")
                {
                    // ApprovalResult was set in EvaluateAsync (which runs before CreateAnswerAsync)
                    var approvalResult = datagram.ApprovalResult ?? WriteApprovalResult.Accept();
                    return ResultData.FromApprovalResult(approvalResult);
                }
                else
                {
                    return null;
                }
            }

            private async Task<WriteApprovalResult> GetApprovalAsync(Connection connection, string limitDirection, ActiveLimitWriteRequest request)
            {
                if (limitDirection == "consume")
                {
                    List<LPCEvents> handlers = connection.Local.GetUseCaseEvents<LPCEvents>();
                    if (handlers.Count == 0)
                        return WriteApprovalResult.Accept("No handlers registered - auto-approved");

                    foreach (var handler in handlers)
                    {
                        var result = await handler.ApproveActiveLimitWriteAsync(request);
                        if (!result.Approved)
                            return result;
                    }
                    return WriteApprovalResult.Accept();
                }
                else if (limitDirection == "produce")
                {
                    List<LPPEvents> handlers = connection.Local.GetUseCaseEvents<LPPEvents>();
                    if (handlers.Count == 0)
                        return WriteApprovalResult.Accept("No handlers registered - auto-approved");

                    foreach (var handler in handlers)
                    {
                        var result = await handler.ApproveActiveLimitWriteAsync(request);
                        if (!result.Approved)
                            return result;
                    }
                    return WriteApprovalResult.Accept();
                }

                return WriteApprovalResult.Deny("Unknown limit direction");
            }

            public override async ValueTask EvaluateAsync(Connection connection, DatagramType datagram)
            {
                if (datagram.header.cmdClassifier != "write")
                    return;

                if (!connection.BindingAndSubscriptionManager.HasBinding(datagram.header.addressSource, datagram.header.addressDestination))
                {
                    //Reject
                    return;
                }

                var command = datagram.payload == null
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<LoadControlLimitListData>(datagram.payload);
                if (command == null || command.cmd == null || command.cmd.Length == 0)
                    return;

                LoadControlLimitListDataFilterType[]? filter = command.cmd[0].filter;

                if (filter != null)
                {
                    foreach (LoadControlLimitListDataFilterType filterValue in filter)
                    {
                        if (filterValue.cmdControl?.delete != null && filterValue.loadControlLimitListDataSelectors?.limitId != null)
                        {
                            LoadControlLimitDataStructure? filterSelectorData = connection.Local.GetDataStructure<LoadControlLimitDataStructure>(filterValue.loadControlLimitListDataSelectors.limitId);

                            JsonObject? filterSelectorDataJson = JsonSerializer.SerializeToNode(filterSelectorData?.Data)?.AsObject();
                            JsonObject? filterElementDataJson = JsonSerializer.SerializeToNode(filterValue.loadControlLimitDataElements)?.AsObject();

                            if (filterSelectorDataJson != null && filterElementDataJson != null)
                            {
                                foreach (var item in filterElementDataJson)
                                {
                                    //remove every property that is set in the filter data elements
                                    if (item.Value != null)
                                    {
                                        filterSelectorDataJson.Remove(item.Key);
                                    }
                                }

                                LoadControlLimitDataType? newData = JsonSerializer.Deserialize<LoadControlLimitDataType>(filterSelectorDataJson) ?? throw new Exception("Error parsing data structure");
                                filterSelectorData?.Update(newData);
                            }
                        }
                    }
                }

                WriteApprovalResult approvalResult = WriteApprovalResult.Accept();
                foreach (LoadControlLimitDataType loadControlLimitData in command.cmd[0].loadControlLimitListData.loadControlLimitData)
                {
                    if (loadControlLimitData.limitId == null)
                    {
                        datagram.ApprovalResult = WriteApprovalResult.Deny("Invalid limit ID");
                        return;
                    }

                    LoadControlLimitDataStructure? data = connection.Local.GetDataStructure<LoadControlLimitDataStructure>(loadControlLimitData.limitId.Value);
                    if (data == null)
                    {
                        datagram.ApprovalResult = WriteApprovalResult.Deny("Unknown limit ID");
                        return;
                    }

                    PowerDirection direction = data.LimitDirection == "consume"
                        ? PowerDirection.Consumption
                        : PowerDirection.Production;

                    TimeSpan? duration = null;
                    if (!string.IsNullOrEmpty(loadControlLimitData.timePeriod?.endTime))
                    {
                        try { duration = XmlConvert.ToTimeSpan(loadControlLimitData.timePeriod.endTime); }
                        catch
                        {
                            /* return error for parsing errors */
                            datagram.ApprovalResult = WriteApprovalResult.Deny("invalid timePeriod.endTime");
                            return;
                        }
                    }

                    var request = new ActiveLimitWriteRequest(
                        direction,
                        loadControlLimitData.isLimitActive ?? false,
                        loadControlLimitData.value?.number ?? 0,
                        loadControlLimitData.value?.scale ?? 0,
                        duration,
                        connection.Remote?.DeviceId,
                        connection.Remote?.SKI?.ToString()
                    );

                    // User-Callback aufrufen
                    if (approvalResult.Approved)
                    {
                        approvalResult = await GetApprovalAsync(connection, data.LimitDirection, request);
                    }
                }

                datagram.ApprovalResult = approvalResult;
                if (approvalResult.Approved)
                {
                    // Only update data if all limits are approved
                    foreach (LoadControlLimitDataType loadControlLimitData in command.cmd[0].loadControlLimitListData.loadControlLimitData)
                    {
                        if (loadControlLimitData.limitId == null)
                        {
                            // checked above, cannot occur
                            return;
                        }

                        LoadControlLimitDataStructure? data = connection.Local.GetDataStructure<LoadControlLimitDataStructure>(loadControlLimitData.limitId.Value);
                        if (data == null)
                        {
                            // checked above, cannot occur
                            return;
                        }
                        data.Update(loadControlLimitData);

                        await data.SendEventAsync(connection);
                    }
                    await SendNotifyAsync(connection.Local, datagram.header.addressDestination);
                }
            }


            public override async Task WriteDataAsync(LocalDevice device, DeviceData deviceData)
            {
                bool didChange = false;

                if (deviceData.Lpc != null && !deviceData.Lpc.IsEmpty())
                {
                    var consumeDataStructures = device.GetDataStructures<LoadControlLimitDataStructure>().Where(ds => ds.LimitDirection == "consume");
                    foreach (var consumeDataStructure in consumeDataStructures)
                    {
                        consumeDataStructure.LimitActive = deviceData.Lpc.LimitActive ?? consumeDataStructure.LimitActive;
                        consumeDataStructure.Number = deviceData.Lpc.Limit ?? consumeDataStructure.Number;
                        consumeDataStructure.EndTime = deviceData.Lpc.LimitDuration != null ? XmlConvert.ToString(TimeSpan.FromSeconds(deviceData.Lpc.LimitDuration.Value)) : consumeDataStructure.EndTime;
                    }
                    didChange = consumeDataStructures.Count() > 0;
                }

                if (deviceData.Lpp != null && !deviceData.Lpp.IsEmpty())
                {
                    var produceDataStructures = device.GetDataStructures<LoadControlLimitDataStructure>().Where(ds => ds.LimitDirection == "produce");
                    foreach (var produceDataStructure in produceDataStructures)
                    {
                        produceDataStructure.LimitActive = deviceData.Lpp.LimitActive ?? produceDataStructure.LimitActive;
                        produceDataStructure.Number = deviceData.Lpp.Limit ?? produceDataStructure.Number;
                        produceDataStructure.EndTime = deviceData.Lpp.LimitDuration != null ? XmlConvert.ToString(TimeSpan.FromSeconds(deviceData.Lpp.LimitDuration.Value)) : produceDataStructure.EndTime;
                    }
                    didChange = produceDataStructures.Count() > 0;
                }

                if (didChange)
                {
                    await SendNotifyAsync(device, device.GetFeatureAddress("LoadControl", true));
                }
            }

            public override JsonNode? CreateNotifyPayload(LocalDevice localDevice)
            {
                LoadControlLimitListData limitData = new LoadControlLimitListData();
                LoadControlLimitListDataType data = limitData.cmd[0].loadControlLimitListData;

                List<LoadControlLimitDataType> datas = new();
                foreach (LoadControlLimitDataStructure structure in localDevice.GetDataStructures<LoadControlLimitDataStructure>())
                {
                    datas.Add(structure.Data);
                }
                data.loadControlLimitData = datas.ToArray();
                return limitData.ToJsonNode();
            }
        }
    }


    [System.SerializableAttribute()]
    public class CmdLoadControlLimitListDataType : CmdType
    {
        public LoadControlLimitListDataFilterType[]? filter { get; set; }
        public LoadControlLimitListDataType loadControlLimitListData { get; set; } = new();
    }

    [System.SerializableAttribute()]
    public class LoadControlLimitListDataType
    {
        public LoadControlLimitDataType[] loadControlLimitData { get; set; }
    }

    [System.SerializableAttribute()]
    public class LoadControlLimitListDataFilterType : FilterType
    {
        public LoadControlLimitListDataSelectorType? loadControlLimitListDataSelectors { get; set; }
        public LoadControlLimitDataElementsType? loadControlLimitDataElements { get; set; }
    }


    [System.SerializableAttribute()]
    public class LoadControlLimitDataType
    {
        public uint? limitId { get; set; }

        public bool? isLimitChangeable { get; set; }

        public bool? isLimitActive { get; set; }

        public TimePeriodType? timePeriod { get; set; }

        public ScaledNumberType? value { get; set; }
    }

    [System.SerializableAttribute()]
    public class LoadControlLimitListDataSelectorType
    {
        public uint limitId { get; set; }
    }

    [System.SerializableAttribute()]
    public class LoadControlLimitDataElementsType
    {
        public object? limitId { get; set; }
        public object? isLimitChangeable { get; set; }

        public object? isLimitActive { get; set; }

        public object? timePeriod { get; set; }

        public object? value { get; set; }
    }

    [System.SerializableAttribute()]
    public class TimePeriodType
    {
        [JsonPropertyName("startTime")]
        public string startTime { get; set; }

        [JsonPropertyName("endTime")]
        public string endTime { get; set; }
    }
}
