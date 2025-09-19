using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.Features
{
	public class DeviceDiagnosisServerFeature : Feature
	{
		static DeviceDiagnosisServerFeature()
		{
			Register( "DeviceDiagnosis-server", new Class() );
		}

		public DeviceDiagnosisServerFeature( Entity owner )
			: base( "DeviceDiagnosis", "server", owner )
		{
			this.Functions.Add( new Function( "deviceDiagnosisHeartbeatData", true, false ) );
		}

		public DeviceDiagnosisServerFeature( int index, Entity owner, FeatureInformationType featureInfo )
			: base( index, "DeviceDiagnosis", "server", owner, featureInfo )
		{
		}

		public new class Class : Feature.Class
		{
			public override Feature Create( Entity owner )
			{
				return new DeviceDiagnosisServerFeature( owner );
			}

			public override Feature Create( int index, Entity owner, FeatureInformationType featureInfo )
			{
				return new DeviceDiagnosisServerFeature( index, owner, featureInfo  );
			}
		}

		public override string Description
		{
			get
			{
				return "DeviceDiagnosis Server";
			}
		}
	}
}
