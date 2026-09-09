using EEBUS.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.SPINE.Commands
{
    public class NodeManagementSubscriptionData : SpineCmdPayload<CmdNodeManagementSubscriptionDataType>
    {
        static NodeManagementSubscriptionData()
        {
            Register("nodeManagementSubscriptionData", new Class());
        }

        public new class Class : SpineCmdPayload<CmdNodeManagementSubscriptionDataType>.Class
        {
            public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync(DatagramType datagram, HeaderType header, Connection connection)
            {
                if (datagram.header.cmdClassifier != "read")
                    return null;

                NodeManagementSubscriptionData data = new NodeManagementSubscriptionData();
                data.cmd[0].nodeManagementSubscriptionData.subscriptionEntry = connection.BindingAndSubscriptionManager.GetSubscriptions().Select(s => new NodeManagementSubscriptionEntryDataType()
                {
                    clientAddress = s.clientAddress,
                    serverAddress = s.serverAddress
                }).ToList();

                return data;
            }
            public override SpineCmdPayloadBase CreateRead(Connection connection)
            {
                return new NodeManagementSubscriptionData();
            }
        }
    }

    public class CmdNodeManagementSubscriptionDataType : CmdType
    {
        public NodeManagementSubscriptionDataType nodeManagementSubscriptionData { get; set; } = new();
    }

    public class NodeManagementSubscriptionDataType
    {
        public List<NodeManagementSubscriptionEntryDataType> subscriptionEntry { get; set; } = new();
    }

    public class NodeManagementSubscriptionEntryDataType
    {
        public AddressType clientAddress { get; set; } = new();
        public AddressType serverAddress { get; set; } = new();
    }
}
