
using Newtonsoft.Json;

using EEBUS.Messages;
using EEBUS.UseCases;

namespace EEBUS.SPINE.Commands
{
	public class ResultData : SpineCmdPayload<CmdResultDataType>
	{
		static ResultData()
		{
			Register( "resultData", new Class() );
		}

		public ResultData()
		{
		}

		public ResultData( int errorNumber, string description = null )
		{
			this.cmd[0].resultData.errorNumber  = errorNumber;
			this.cmd[0].resultData.description  = description;
		}

		public static ResultData FromApprovalResult( WriteApprovalResult result )
		{
			return new ResultData( result.ErrorCode, result.Description );
		}

		public new class Class : SpineCmdPayload<CmdResultDataType>.Class
		{
			public override SpineCmdPayloadBase CreateAnswer( DatagramType datagram, HeaderType header, Connection connection )
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

		[JsonProperty( NullValueHandling = NullValueHandling.Ignore )]
		public string description { get; set; }
	}
}
