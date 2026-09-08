using EEBUS.Messages;
using EEBUS.SPINE.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Spine.Commands
{
    public class NodeManagementSubscriptionDeleteCall : SpineCmdPayload<CmdNodeManagementSubscriptionDeleteCallType>
    {
        static NodeManagementSubscriptionDeleteCall()
        {
            Register("nodeManagementSubscriptionDeleteCall", new Class());
        }

        public new class Class : SpineCmdPayload<CmdNodeManagementSubscriptionDeleteCallType>.Class
        {
            public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync(DatagramType datagram, HeaderType header, Connection connection)
            {
                if (datagram.header.cmdClassifier != "call")
                    return null;
                bool success = false;
                var subscriptionDeleteReq = FromJsonNode(datagram.payload);
                if (subscriptionDeleteReq != null && subscriptionDeleteReq.cmd.FirstOrDefault()?.nodeManagementSubscriptionDeleteCall.subscriptionDelete is SubscriptionDeleteType req)
                {
                    success = connection.BindingAndSubscriptionManager.TryRemoveSubscription(req.clientAddress, req.serverAddress);
                }
                if (success)
                {
                    ResultData payload = new ResultData();
                    return payload;
                }
                //Reject
                return null;
            }
            public override SpineCmdPayloadBase CreateCall(Connection connection)
            {
                return new NodeManagementSubscriptionDeleteCall();
            }
        }
    }

    [System.SerializableAttribute()]
    public class CmdNodeManagementSubscriptionDeleteCallType : CmdType
    {
        public NodeManagementSubscriptionDeleteCallType nodeManagementSubscriptionDeleteCall { get; set; } = new();
    }

    [System.SerializableAttribute()]
    public class NodeManagementSubscriptionDeleteCallType
    {
        public SubscriptionDeleteType subscriptionDelete { get; set; } = new();
    }

    [System.SerializableAttribute()]
    public class SubscriptionDeleteType
    {
        public AddressType clientAddress { get; set; } = new();
        public AddressType serverAddress { get; set; } = new();
    }
}
