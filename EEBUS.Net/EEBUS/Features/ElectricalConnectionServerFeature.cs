using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.Features
{
	public class ElectricalConnectionServerFeature : Feature
	{
		static ElectricalConnectionServerFeature()
		{
			Register( "ElectricalConnection-server", new Class() );
		}

		public ElectricalConnectionServerFeature( Entity owner )
			: base( "ElectricalConnection", "server", owner )
		{
			// LPC, LPP, MGCP
			this.Functions.Add( new Function( "electricalConnectionCharacteristicListData",		  true, false ) );

			// MGCP
			this.Functions.Add( new Function( "electricalConnectionDescriptionListData",		  true, false ) );
			this.Functions.Add( new Function( "electricalConnectionParameterDescriptionListData", true, false ) );
		}

		public ElectricalConnectionServerFeature( int index, Entity owner, FeatureInformationType featureInfo )
			: base( index, "ElectricalConnection", "server", owner, featureInfo )
		{
		}

		public new class Class : Feature.Class
		{
			public override Feature Create( Entity owner )
			{
				return new ElectricalConnectionServerFeature( owner );
			}

			public override Feature Create( int index, Entity owner, FeatureInformationType featureInfo )
			{
				return new ElectricalConnectionServerFeature( index, owner, featureInfo  );
			}
		}

		public override string Description
		{
			get
			{
				return "ElectricalConnection Server";
			}
		}
	}
}
