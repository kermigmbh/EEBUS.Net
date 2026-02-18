using System.Diagnostics;
using System.Xml;

using System.Text.Json.Serialization;

using EEBUS.DataStructures;
using EEBUS.Messages;
using EEBUS.SHIP.Messages;
using EEBUS.UseCases.ControllableSystem;
using EEBUS.KeyValues;
using System.Text.Json.Nodes;
using System.Text.Json;
using EEBUS.Models;

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
                    return new ResultData();    // Alternative: send Error 7 and a text message
                }
                else
                {
                    return null;
                }
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

                LoadControlLimitDataType received = command.cmd[0].loadControlLimitListData.loadControlLimitData[0];
                
                LoadControlLimitDataStructure? data = connection.Local.GetDataStructure<LoadControlLimitDataStructure>(received.limitId);
                if (data == null)
                    return;

                data.LimitActive = received.isLimitActive;
                // received.value.number is nullable, keep existing number if null
                data.Number = received.value.number ?? data.Number;
                data.EndTime = received.timePeriod.endTime;

                await data.SendEventAsync(connection);


                if ( connection.BindingAndSubscriptionManager.HasSubscription(datagram.header.addressSource, datagram.header.addressDestination))
                {
                    SendNotify(connection, datagram);
                }
               
            }

            public override Task WriteDataAsync(LocalDevice device, JsonObject data)
            {
                JsonSerializer.Deserialize<LoadControlLimitListData>(data);
                return Task.CompletedTask;
            }

            private void SendNotify(Connection connection, DatagramType datagram)
            {
                SpineDatagramPayload notify = new SpineDatagramPayload();
                notify.datagram.header.addressSource = datagram.header.addressDestination;
                notify.datagram.header.addressDestination = datagram.header.addressSource;
                notify.datagram.header.msgCounter = DataMessage.NextCount;
                notify.datagram.header.cmdClassifier = "notify";

                LoadControlLimitListData limitData = new LoadControlLimitListData();
                LoadControlLimitListDataType data = limitData.cmd[0].loadControlLimitListData;

                List<LoadControlLimitDataType> datas = new();

                //if (connection.Remote?.HeartbeatValidUntil < DateTime.UtcNow.AddMinutes(2))
                //{
                //    var failSafe = connection.Local.GetKeyValue<FailsafeConsumptionActivePowerLimitKeyValue>();
                //    foreach (LoadControlLimitDataStructure structure in connection.Local.GetDataStructures<LoadControlLimitDataStructure>())
                //    {
                //        structure.Number = failSafe.Value;
                //        datas.Add(structure.Data);
                //    }

                //}
                //else
                //{
                foreach (LoadControlLimitDataStructure structure in connection.Local.GetDataStructures<LoadControlLimitDataStructure>())
                {
                    datas.Add(structure.Data);
                }
                //}

                data.loadControlLimitData = datas.ToArray();
                notify.datagram.payload = limitData.ToJsonNode();

                DataMessage limitMessage = new DataMessage();
                limitMessage.SetPayload(System.Text.Json.JsonSerializer.SerializeToNode(notify));

                connection.PushDataMessage(limitMessage);
            }
        }
    }


    [System.SerializableAttribute()]
    public class CmdLoadControlLimitListDataType : CmdType
    {
        public LoadControlLimitListDataType loadControlLimitListData { get; set; } = new();
    }

    [System.SerializableAttribute()]
    public class LoadControlLimitListDataType
    {
        public LoadControlLimitDataType[] loadControlLimitData { get; set; }
    }

    [System.SerializableAttribute()]
    public class LoadControlLimitDataType
    {
        public uint limitId { get; set; }

        public bool isLimitChangeable { get; set; }

        public bool isLimitActive { get; set; }

        public TimePeriodType timePeriod { get; set; } = new();

        public ScaledNumberType value { get; set; } = new();
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
