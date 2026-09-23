
using EEBUS.Messages;

namespace EEBUS.SPINE.Commands
{
	public class NodeManagementBindingRequestCall : SpineCmdPayload<CmdNodeManagementBindingRequestCallType>
	{
		static NodeManagementBindingRequestCall()
		{
			Register( "nodeManagementBindingRequestCall", new Class() );
		}

		public new class Class : SpineCmdPayload<CmdNodeManagementBindingRequestCallType>.Class
		{
			public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync( DatagramType datagram, HeaderType header, Connection connection )
			{
                if (datagram.header.cmdClassifier != "call")
                    return null;

                bool success = false;
				string serverFeatureType = string.Empty;
                var bindingReq = FromJsonNode(datagram.payload);
                if (bindingReq != null && bindingReq.cmd.FirstOrDefault()?.nodeManagementBindingRequestCall.bindingRequest is BindingRequestType req)
                {
					serverFeatureType = req.serverFeatureType;
                    success = connection.BindingAndSubscriptionManager.TryAddOrUpdateClientBinding(req.clientAddress, req.serverAddress, req.serverFeatureType, Net.BindingSubscriptionDirection.Incoming);
                }


				if (success)
				{
					if (serverFeatureType == "LoadControl")
					{
						connection.HeartbeatSubscription();
					}
                    ResultData payload = new ResultData();
                    return payload;
                }
                //Reject
                return null;
            }

            public override SpineCmdPayloadBase? CreateCall(Connection connection, AddressType clientAddress, AddressType serverAddress, string serverFeatureType = "")
            {
                var call = new NodeManagementBindingRequestCall();
                call.cmd[0].nodeManagementBindingRequestCall.bindingRequest.clientAddress = clientAddress;
                call.cmd[0].nodeManagementBindingRequestCall.bindingRequest.serverAddress = serverAddress;
                call.cmd[0].nodeManagementBindingRequestCall.bindingRequest.serverFeatureType = serverFeatureType;
                connection.BindingAndSubscriptionManager.TryAddOrUpdateClientBinding(clientAddress, serverAddress, serverFeatureType, Net.BindingSubscriptionDirection.Outgoing);

                return call;
            }
        }
	}

	[System.SerializableAttribute()]
	public class CmdNodeManagementBindingRequestCallType : CmdType
	{
		public NodeManagementBindingRequestCallType nodeManagementBindingRequestCall { get; set; } = new();
	}

	[System.SerializableAttribute()]
	public class NodeManagementBindingRequestCallType
	{
		public BindingRequestType bindingRequest { get; set; } = new();
	}

	[System.SerializableAttribute()]
	public class BindingRequestType
	{
		public AddressType clientAddress	 { get; set; }

		public AddressType serverAddress	 { get; set; }

		public string	   serverFeatureType { get; set; }
	}
}
