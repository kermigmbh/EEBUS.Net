using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.Features
{
	public class DeviceDiagnosisClientFeature : Feature
	{
		static DeviceDiagnosisClientFeature()
		{
			Register( "DeviceDiagnosis-client", new Class() );
		}

		public DeviceDiagnosisClientFeature( Entity owner )
			: base( "DeviceDiagnosis", "client", owner )
		{
		}

		public DeviceDiagnosisClientFeature( int index, Entity owner, FeatureInformationType featureInfo )
			: base( index, "DeviceDiagnosis", "client", owner, featureInfo )
		{
		}

		public new class Class : Feature.Class
		{
			public override Feature Create( Entity owner )
			{
				return new DeviceDiagnosisClientFeature( owner );
			}

			public override Feature Create( int index, Entity owner, FeatureInformationType featureInfo )
			{
				return new DeviceDiagnosisClientFeature( index, owner, featureInfo  );
			}
		}

		public override string Description
		{
			get
			{
				return "DeviceDiagnosis Client";
			}
		}
	}
}
