using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.Entities
{
	public class GridGuardEntity : Entity
	{
		static GridGuardEntity()
		{
			Register( "GridGuard", new Class() );
		}

		public GridGuardEntity( int index, LocalDevice local, EntitySettings entitySettings )
			: base( index, local, entitySettings )
		{
			GetOrAdd( Feature.Create( "DeviceDiagnosis",	  "client", this ) );
			GetOrAdd( Feature.Create( "LoadControl",		  "client", this ) );
			GetOrAdd( Feature.Create( "DeviceConfiguration",  "client", this ) );
			GetOrAdd( Feature.Create( "ElectricalConnection", "client", this ) );
			GetOrAdd( Feature.Create( "DeviceDiagnosis",	  "server", this ) );
		}

		public GridGuardEntity( int index, LocalDevice local, EntityInformationType entityInfo, FeatureInformationType[] featureInfos )
			: base( index, local, entityInfo, featureInfos )
		{
		}


		public new class Class : Entity.Class
		{
			public override Entity Create( int index, LocalDevice local, EntitySettings entitySettings )
			{
				return new GridGuardEntity( index, local, entitySettings );
			}

			public override Entity Create( int index, LocalDevice local, EntityInformationType entityInfo, FeatureInformationType[] featureInfos )
			{
				return new GridGuardEntity( index, local, entityInfo, featureInfos );
			}
		}
	}
}
