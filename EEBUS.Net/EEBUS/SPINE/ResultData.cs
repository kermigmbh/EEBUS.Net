
using EEBUS.Messages;

namespace EEBUS.SPINE.Commands
{
	public class ResultData : SpineCmdPayload<CmdResultDataType>
	{
		static ResultData()
		{
			Register( "resultData", new Class() );
		}

		public new class Class : SpineCmdPayload<CmdResultDataType>.Class
		{
			public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync( DatagramType datagram, HeaderType header, Connection connection )
			{
				return null;
			}
		}
	}

	[System.SerializableAttribute()]
	public class CmdResultDataType : CmdType
	{
		public ResultDataType resultData { get; set; } = new();
	}

	[System.SerializableAttribute()]
	public class ResultDataType
	{
		public int errorNumber { get; set; } = 0;
	}
}
