
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

            //public override async ValueTask EvaluateAsync(Connection connection, DatagramType datagram)
            //{
               
            //}

			public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync( DatagramType datagram, HeaderType header, Connection connection )
			{

				bool success = false;
                var bindingReq = FromJsonNode(datagram.payload);
                if (bindingReq != null && bindingReq.cmd.FirstOrDefault()?.nodeManagementBindingRequestCall.bindingRequest is BindingRequestType req)
                {
                    success = connection.BindingAndSubscriptionManager.TryAddOrUpdateClientBinding(req.clientAddress, req.serverAddress, req.serverFeatureType);
                }


				if (success)
				{
                   
                }
                //Reject
                ResultData payload = new ResultData();

                return payload;

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
