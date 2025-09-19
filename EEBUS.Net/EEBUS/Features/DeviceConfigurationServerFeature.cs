using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.Features
{
	public class DeviceConfigurationServerFeature : Feature
	{
		static DeviceConfigurationServerFeature()
		{
			Register( "DeviceConfiguration-server", new Class() );
		}

		public DeviceConfigurationServerFeature( Entity owner )
			: base( "DeviceConfiguration", "server", owner )
		{
			this.Functions.Add( new Function( "deviceConfigurationKeyValueDescriptionListData",	true, false ) );
			this.Functions.Add( new Function( "deviceConfigurationKeyValueListData",			true, true ) );
		}

		public DeviceConfigurationServerFeature( int index, Entity owner, FeatureInformationType featureInfo )
			: base( index, "DeviceClassification", "server", owner, featureInfo )
		{
		}

		public new class Class : Feature.Class
		{
			public override Feature Create( Entity owner )
			{
				return new DeviceConfigurationServerFeature( owner );
			}

			public override Feature Create( int index, Entity owner, FeatureInformationType featureInfo )
			{
				return new DeviceConfigurationServerFeature( index, owner, featureInfo  );
			}
		}

		public override string Description
		{
			get
			{
				return "DeviceConfiguration Server";
			}
		}
	}
}
