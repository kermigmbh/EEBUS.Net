


using EEBUS.Messages;

namespace EEBUS.SPINE.Commands
{
	public class NodeManagementSubscriptionRequestCall : SpineCmdPayload<CmdNodeManagementSubscriptionRequestCallType>
	{
		static NodeManagementSubscriptionRequestCall()
		{
			Register( "nodeManagementSubscriptionRequestCall", new Class() );
		}

		public new class Class : SpineCmdPayload<CmdNodeManagementSubscriptionRequestCallType>.Class
		{
			public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync( DatagramType datagram, HeaderType header, Connection connection )
			{


                bool success = false;
                var subscriptionReq = FromJsonNode(datagram.payload);
                if (subscriptionReq != null && subscriptionReq.cmd.FirstOrDefault()?.nodeManagementSubscriptionRequestCall.subscriptionRequest is SubscriptionRequestType req)
                {
                    success = connection.BindingAndSubscriptionManager.TryAddOrUpdateSubscription(req.clientAddress, req.serverAddress, req.serverFeatureType);
                }

                if (success)
                {
                    
                }
                //Reject
                ResultData payload = new ResultData();

                return payload;
            }

			public override SpineCmdPayloadBase CreateCall( Connection connection )
			{
				return new NodeManagementSubscriptionRequestCall();
			}

		}
	}

	[System.SerializableAttribute()]
	public class CmdNodeManagementSubscriptionRequestCallType : CmdType
	{
		public NodeManagementSubscriptionRequestCallType nodeManagementSubscriptionRequestCall { get; set; } = new();
	}

	[System.SerializableAttribute()]
	public class NodeManagementSubscriptionRequestCallType
	{
		public SubscriptionRequestType subscriptionRequest { get; set; } = new();
	}

	[System.SerializableAttribute()]
	public class SubscriptionRequestType
	{
		public AddressType clientAddress	 { get; set; }

		public AddressType serverAddress	 { get; set; }

		public string	   serverFeatureType { get; set; }
	}
}
