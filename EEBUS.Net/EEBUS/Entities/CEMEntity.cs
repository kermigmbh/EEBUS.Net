using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.Entities
{
	public class CEMEntity : Entity
	{
		static CEMEntity()
		{
			Register( "CEM", new Class() );
		}

		public CEMEntity( int index, LocalDevice local, EntitySettings entitySettings )
			: base( index, local, entitySettings )
		{
		}

		public CEMEntity( int index, LocalDevice local, EntityInformationType entityInfo, FeatureInformationType[] featureInfos )
			: base( index, local, entityInfo, featureInfos )
		{
		}


		public new class Class : Entity.Class
		{
			public override Entity Create( int index, LocalDevice local, EntitySettings entitySettings )
			{
				return new CEMEntity( index, local, entitySettings );
			}

			public override Entity Create( int index, LocalDevice local, EntityInformationType entityInfo, FeatureInformationType[] featureInfos )
			{
				return new CEMEntity( index, local, entityInfo, featureInfos );
			}
		}
	}
}
