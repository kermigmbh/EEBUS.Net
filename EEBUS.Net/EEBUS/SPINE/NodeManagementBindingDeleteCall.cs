using EEBUS.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.SPINE.Commands
{
    public class NodeManagementBindingDeleteCall : SpineCmdPayload<CmdNodeManagementBindingDeleteCallType>
    {
        static NodeManagementBindingDeleteCall()
        {
            Register("nodeManagementBindingDeleteCall", new Class());
        }

        public new class Class : SpineCmdPayload<CmdNodeManagementBindingDeleteCallType>.Class
        {
            public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync(DatagramType datagram, HeaderType header, Connection connection)
            {
                if (datagram.header.cmdClassifier != "call")
                    return null;

                bool success = false;
                var bindingDeleteReq = FromJsonNode(datagram.payload);
                if (bindingDeleteReq != null && bindingDeleteReq.cmd.FirstOrDefault()?.nodeManagementBindingDeleteCall.bindingDelete is BindingDeleteType req)
                {
                    success = connection.BindingAndSubscriptionManager.TryRemoveBinding(req.clientAddress, req.serverAddress);
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
                return new NodeManagementBindingDeleteCall();
            }
        }
    }

    public class CmdNodeManagementBindingDeleteCallType : CmdType
    {
        public NodeManagementBindingDeleteCallType nodeManagementBindingDeleteCall { get; set; } = new();
    }

    public class NodeManagementBindingDeleteCallType
    {
        public BindingDeleteType bindingDelete { get; set; } = new();
    }

    public class BindingDeleteType
    {
        public AddressType clientAddress { get; set; } = new();
        public AddressType serverAddress { get; set; } = new();
    }
}
