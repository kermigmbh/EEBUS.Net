using EEBUS;
using EEBUS.Messages;
using EEBUS.SHIP.Messages;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TestProject1
{
    public class SpineTests : EebusTests
    {
        private static SpineCmdPayloadBase? GetCommand(ShipMessageBase? m)
        {
            if (m is DataMessage dataMessage)
            {
                if (dataMessage.data.payload is JsonObject payloadObj && payloadObj.ContainsKey("datagram"))
                {
                    SpineDatagramPayload? payload = dataMessage.data.payload.Deserialize<SpineDatagramPayload>();

                    var spineCmd = payload?.DeserializePayload();
                    return spineCmd;

                }
            }
            return null;    
        }

        [Fact]
        public void Test1()
        {


            var m = ShipMessageBase.Create(Encoding.UTF8.GetBytes(EEBusMessages.MsgNodeManagementDetailedDiscoveryDataReply));
            var cmd = GetCommand(m);

            m = ShipMessageBase.Create(Encoding.UTF8.GetBytes(EEBusMessages.MsgNodeManagementBindingRequestCall));
            cmd = GetCommand(m);
        }

      
    }
}
