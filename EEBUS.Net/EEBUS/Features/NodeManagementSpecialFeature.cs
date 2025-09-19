using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.Features
{
	public class NodeManagementSpecialFeature : Feature
	{
		static NodeManagementSpecialFeature()
		{
			Register( "NodeManagement-special", new Class() );
		}

		public NodeManagementSpecialFeature( Entity owner )
			: base( "NodeManagement", "special", owner )
		{
			this.Functions.Add( new Function( "nodeManagementBindingData",			   true,  false ) );
			this.Functions.Add( new Function( "nodeManagementDetailedDiscoveryData",   true,  false ) );
			this.Functions.Add( new Function( "nodeManagementUseCaseData",			   true,  false ) );
			this.Functions.Add( new Function( "nodeManagementSubscriptionDeleteCall",  false, false ) );
			this.Functions.Add( new Function( "nodeManagementBindingDeleteCall",	   false, false ) );
			this.Functions.Add( new Function( "nodeManagementDestinationListData",	   true,  false ) );
			this.Functions.Add( new Function( "nodeManagementSubscriptionData",		   true,  false ) );
			this.Functions.Add( new Function( "nodeManagementSubscriptionRequestCall", false, false ) );
			this.Functions.Add( new Function( "nodeManagementBindingRequestCall",	   false, false ) );
		}

		public NodeManagementSpecialFeature( int index, Entity owner, FeatureInformationType featureInfo )
			: base( index, "NodeManagement", "special", owner, featureInfo )
		{
		}

		public new class Class : Feature.Class
		{
			public override Feature Create( Entity owner )
			{
				return new NodeManagementSpecialFeature( owner );
			}

			public override Feature Create( int index, Entity owner, FeatureInformationType featureInfo )
			{
				return new NodeManagementSpecialFeature( index, owner, featureInfo  );
			}
		}
	}
}
