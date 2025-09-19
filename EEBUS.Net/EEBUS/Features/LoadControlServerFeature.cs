using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.Features
{
	public class LoadControlServerFeature : Feature
	{
		static LoadControlServerFeature()
		{
			Register( "LoadControl-server", new Class() );
		}

		public LoadControlServerFeature( Entity owner )
			: base( "LoadControl", "server", owner )
		{
			this.Functions.Add( new Function( "loadControlLimitDescriptionListData", true, false ) );
			this.Functions.Add( new Function( "loadControlLimitListData",			 true, true ) );
		}

		public LoadControlServerFeature( int index, Entity owner, FeatureInformationType featureInfo )
			: base( index, "LoadControl", "server", owner, featureInfo )
		{
		}

		public new class Class : Feature.Class
		{
			public override Feature Create( Entity owner )
			{
				return new LoadControlServerFeature( owner );
			}

			public override Feature Create( int index, Entity owner, FeatureInformationType featureInfo )
			{
				return new LoadControlServerFeature( index, owner, featureInfo  );
			}
		}

		public override string Description
		{
			get
			{
				return "LoadControl Server";
			}
		}
	}
}
