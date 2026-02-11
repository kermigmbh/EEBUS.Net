
using System.Diagnostics;
using System.Xml;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using EEBUS.DataStructures;
using EEBUS.Messages;
using EEBUS.SHIP.Messages;
using EEBUS.UseCases;
using EEBUS.UseCases.ControllableSystem;

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
				else if ( datagram.header.cmdClassifier == "write" )
				{
					var approvalResult = datagram.ApprovalResult;
					return ResultData.FromApprovalResult( approvalResult );
				}
				else
				{
					return null;
				}
			}

            private WriteApprovalResult GetApproval( Connection connection, string limitDirection, ActiveLimitWriteRequest request )
            {
                if ( limitDirection == "consume" )
                {
                    List<LPCEvents> handlers = connection.Local.GetUseCaseEvents<LPCEvents>();
                    if ( handlers.Count == 0 )
                        return WriteApprovalResult.Accept( "No handlers registered - auto-approved" );

                    foreach ( var handler in handlers )
                    {
                        var result = handler.ApproveActiveLimitWrite( request );
                        if ( !result.Approved )
                            return result;
                    }
                    return WriteApprovalResult.Accept();
                }
                else if ( limitDirection == "produce" )
                {
                    List<LPPEvents> handlers = connection.Local.GetUseCaseEvents<LPPEvents>();
                    if ( handlers.Count == 0 )
                        return WriteApprovalResult.Accept( "No handlers registered - auto-approved" );

                    foreach ( var handler in handlers )
                    {
                        var result = handler.ApproveActiveLimitWrite( request );
                        if ( !result.Approved )
                            return result;
                    }
                    return WriteApprovalResult.Accept();
                }

                return WriteApprovalResult.Deny( "Unknown limit direction" );
            }
            
            public override async ValueTask EvaluateAsync(Connection connection, DatagramType datagram)
            {
                if (datagram.header.cmdClassifier != "write")
                    return;

                var command = datagram.payload == null
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<LoadControlLimitListData>(datagram.payload);
                if (command == null || command.cmd == null || command.cmd.Length == 0)
                    return;

                LoadControlLimitDataType received = command.cmd[0].loadControlLimitListData.loadControlLimitData[0];

                LoadControlLimitDataStructure data = connection.Local.GetDataStructure<LoadControlLimitDataStructure>(received.limitId);
				LoadControlLimitDataStructure structureData = connection.Local.GetDataStructure<LoadControlLimitDataStructure>( received.limitId );
				if ( structureData == null )
				{
					datagram.ApprovalResult = WriteApprovalResult.Deny( "Unknown limit ID" );
					return;
				}

				PowerDirection direction = structureData.LimitDirection == "consume"
					? PowerDirection.Consumption
					: PowerDirection.Production;

                data.LimitActive = received.isLimitActive;
                // received.value.number is nullable, keep existing number if null
                data.Number = received.value.number ?? data.Number;
                data.EndTime = received.timePeriod.endTime;
				TimeSpan? duration = null;
				if ( !string.IsNullOrEmpty( received.timePeriod?.endTime ) )
				{
					try { duration = XmlConvert.ToTimeSpan( received.timePeriod.endTime ); }
					catch { /* ignore parsing errors */ }
				}

                await data.SendEventAsync(connection);
				var request = new ActiveLimitWriteRequest(
					direction,
					received.isLimitActive,
					received.value.number,
					received.value.scale,
					duration,
					connection.Remote?.DeviceId,
					null
				);

                SendNotify(connection, datagram);
            }
				WriteApprovalResult approvalResult = GetApproval( connection, structureData.LimitDirection, request );
				datagram.ApprovalResult = approvalResult;

				if ( approvalResult.Approved )
				{
					structureData.LimitActive = received.isLimitActive;
					structureData.Number      = received.value.number;
					structureData.EndTime     = received.timePeriod.endTime;

					structureData.SendEvent( connection );
					SendNotify( connection, datagram );
				}
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
				List<LoadControlLimitDataType> datas = new();
				foreach ( LoadControlLimitDataStructure structure in connection.Local.GetDataStructures<LoadControlLimitDataStructure>() )
					datas.Add( structure.Data );

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
