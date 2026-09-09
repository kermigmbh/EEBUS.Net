using EEBUS.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.SPINE.Commands
{
    public class NodeManagementBindingData : SpineCmdPayload<CmdNodeManagementBindingDataType>
    {
        static NodeManagementBindingData()
        {
            Register("nodeManagementBindingData", new Class());
        }

        public new class Class : SpineCmdPayload<CmdNodeManagementBindingDataType>.Class
        {
            public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync(DatagramType datagram, HeaderType header, Connection connection)
            {
                if (datagram.header.cmdClassifier != "read") return null;

                NodeManagementBindingData data = new NodeManagementBindingData();
                data.cmd[0].nodeManagementBindingData.bindingEntry = connection.BindingAndSubscriptionManager.GetBindings().Select(b => new NodeManagementBindingEntryDataType
                {
                    clientAddress = b.clientAddress,
                    serverAddress = b.serverAddress
                }).ToList();

                return data;
            }
        }
    }

    public class CmdNodeManagementBindingDataType : CmdType
    {
        public NodeManagementBindingDataType nodeManagementBindingData { get; set; } = new();
    }

    public class NodeManagementBindingDataType
    {
        public List<NodeManagementBindingEntryDataType> bindingEntry { get; set; } = new();
    }

    public class NodeManagementBindingEntryDataType
    {
        public AddressType clientAddress { get; set; } = new();
        public AddressType serverAddress { get; set; } = new();
    }
}
