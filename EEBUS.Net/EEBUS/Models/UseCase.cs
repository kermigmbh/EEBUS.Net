using EEBUS.SPINE.Commands;

namespace EEBUS.Models
{
	public abstract class UseCase
	{
		protected UseCase( UseCaseSettings usecaseSettings, Entity entity )
		{
			this.Type  = usecaseSettings.Type;
			this.Owner = entity;
		}

		public abstract class Class
		{
			public abstract UseCase Create( UseCaseSettings usecaseSettings, Entity entity );
		}

		static public UseCase Create( UseCaseSettings usecaseSettings, Entity entity )
		{
			if ( usecasesClasses.TryGetValue( usecaseSettings.Type + "-" + usecaseSettings.Actor, out Class cls ) )
				return cls.Create( usecaseSettings, entity );

			return null;
		}

		static protected Dictionary<string, Class> usecasesClasses = new Dictionary<string, Class>();

		static protected void Register( string name, Class cls )
		{
			usecasesClasses.Add( name, cls );
		}


		public string					   Type		   { get; private set; }

		public List<Scenario>			   Scenarios   = new();

		public Entity					   Owner	   { get; private set; }

		public abstract string			   Actor	   { get; }
		public abstract UseCaseSupportType Information { get; }

		public virtual void FillData<T>( List<T> dataList, Connection connection, Entity entity )
		{
		}
	}
}
