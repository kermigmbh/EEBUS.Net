using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.Features
{
	public class LoadControlClientFeature : Feature
	{
		static LoadControlClientFeature()
		{
			Register( "LoadControl-client", new Class() );
		}

		public LoadControlClientFeature( Entity owner )
			: base( "LoadControl", "client", owner )
		{
		}

		public LoadControlClientFeature( int index, Entity owner, FeatureInformationType featureInfo )
			: base( index, "LoadControl", "client", owner, featureInfo )
		{
		}

		public new class Class : Feature.Class
		{
			public override Feature Create( Entity owner )
			{
				return new LoadControlClientFeature( owner );
			}

			public override Feature Create( int index, Entity owner, FeatureInformationType featureInfo )
			{
				return new LoadControlClientFeature( index, owner, featureInfo  );
			}
		}

		public override string Description
		{
			get
			{
				return "LoadControl Client";
			}
		}
	}
}
