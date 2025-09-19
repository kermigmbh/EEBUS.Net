using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.Features
{
	public class DeviceConfigurationClientFeature : Feature
	{
		static DeviceConfigurationClientFeature()
		{
			Register( "DeviceConfiguration-client", new Class() );
		}

		public DeviceConfigurationClientFeature( Entity owner )
			: base( "DeviceConfiguration", "client", owner )
		{
		}

		public DeviceConfigurationClientFeature( int index, Entity owner, FeatureInformationType featureInfo )
			: base( index, "DeviceClassification", "client", owner, featureInfo )
		{
		}

		public new class Class : Feature.Class
		{
			public override Feature Create( Entity owner )
			{
				return new DeviceConfigurationClientFeature( owner );
			}

			public override Feature Create( int index, Entity owner, FeatureInformationType featureInfo )
			{
				return new DeviceConfigurationClientFeature( index, owner, featureInfo  );
			}
		}

		public override string Description
		{
			get
			{
				return "DeviceConfiguration Client";
			}
		}
	}
}
