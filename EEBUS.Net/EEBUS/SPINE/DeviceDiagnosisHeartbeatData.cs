using EEBUS.Messages;
using EEBUS.UseCases.ControllableSystem;
using System.Text.Json.Serialization;
using System.Xml;

namespace EEBUS.SPINE.Commands
{
	public class DeviceDiagnosisHeartbeatData : SpineCmdPayload<CmdDeviceDiagnosisHeartbeatDataType>
	{
		static DeviceDiagnosisHeartbeatData()
		{
			Register( "deviceDiagnosisHeartbeatData", new Class() );
		}

		static public ulong counter = 1;

		public new class Class : SpineCmdPayload<CmdDeviceDiagnosisHeartbeatDataType>.Class
		{
			public override async ValueTask<SpineCmdPayloadBase> CreateAnswerAsync( DatagramType datagram, HeaderType header, Connection connection )
			{
				DeviceDiagnosisHeartbeatData	 payload = new DeviceDiagnosisHeartbeatData();
				DeviceDiagnosisHeartbeatDataType data	 = payload.cmd[0].deviceDiagnosisHeartbeatData;

				data.timestamp		  = DateTime.UtcNow.ToString( "s" ) + "Z";
				data.heartbeatCounter = DeviceDiagnosisHeartbeatData.counter++;
				data.heartbeatTimeout = "PT4S";

				return payload;
			}

			public override SpineCmdPayloadBase CreateNotify( Connection connection )
			{
				DeviceDiagnosisHeartbeatData	 payload = new DeviceDiagnosisHeartbeatData();
				DeviceDiagnosisHeartbeatDataType data	 = payload.cmd[0].deviceDiagnosisHeartbeatData;

				data.timestamp		  = DateTime.UtcNow.ToString( "s" ) + "Z";
				data.heartbeatCounter = DeviceDiagnosisHeartbeatData.counter++;
				data.heartbeatTimeout = "PT4S";

				return payload;
			}

			public override SpineCmdPayloadBase CreateRead( Connection connection )
			{
				return new DeviceDiagnosisHeartbeatData();
			}

			public override async ValueTask EvaluateAsync( Connection connection, DatagramType datagram )
			{
				if ( datagram.header.cmdClassifier != "notify" )
					return;

				DeviceDiagnosisHeartbeatData? payload = datagram.payload == null
					? null
					: System.Text.Json.JsonSerializer.Deserialize<DeviceDiagnosisHeartbeatData>(datagram.payload);
				string timeout = payload.cmd[0].deviceDiagnosisHeartbeatData.heartbeatTimeout;
				
				List<LPCorLPPEvents> lpcOrLppEvents = connection.Local.GetUseCaseEvents<LPCorLPPEvents>();
				foreach (var lpcOrLpp in lpcOrLppEvents)
				{
					lpcOrLpp.DataUpdateHeartbeat(0, connection.Remote, (uint)XmlConvert.ToTimeSpan(timeout).TotalSeconds);
				}
			}
		}
	}

	[System.SerializableAttribute()]
	public class CmdDeviceDiagnosisHeartbeatDataType : CmdType
	{
		public DeviceDiagnosisHeartbeatDataType deviceDiagnosisHeartbeatData { get; set; } = new();
	}

	[System.SerializableAttribute()]
	public class DeviceDiagnosisHeartbeatDataType
	{
		public string? timestamp		   { get; set; }

		public ulong? heartbeatCounter { get; set; }

		public string? heartbeatTimeout { get; set; }
	}
}
