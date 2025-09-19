using EEBUS.SPINE.Commands;

namespace EEBUS.Models
{
	public abstract class Feature
	{
		protected Feature( string type, string role, Entity owner )
		{
			this.Type  = type;
			this.Role  = role;
			this.owner = owner;
		}

		protected Feature( int index, string type, string role, Entity owner, FeatureInformationType featureInfo )
		{
			this.Index = index;
			this.Type  = type;
			this.Role  = role;
			this.owner = owner;

			SupportedFunctionType[] functionInfos = featureInfo.description.supportedFunction;
			if ( null != functionInfos )
			{
				foreach ( SupportedFunctionType functionInfo in functionInfos )
				{
					bool read  = false;
					bool write = false;

					if ( null != functionInfo.possibleOperations )
					{
						read  = null != functionInfo.possibleOperations.read;
						write = null != functionInfo.possibleOperations.write;
					}

					this.Functions.Add( new Function( functionInfo.function,read, write ) );
				}
			}
		}

		public abstract class Class
		{
			public abstract Feature Create( Entity owner );
			public abstract Feature Create( int index, Entity owner, FeatureInformationType featureInfo );
		}

		static public Feature Create( string type, string role, Entity owner )
		{
			if ( featureClasses.TryGetValue( type + "-" + role, out Class cls ) )
				return cls.Create( owner );

			return null;
		}

		static public Feature Create( FeatureInformationType featureInfo, Entity owner )
		{
			int    index = featureInfo.description.featureAddress.feature;
			string type  = featureInfo.description.featureType;
			string role  = featureInfo.description.role;

			if ( featureClasses.TryGetValue( type + "-" + role, out Class cls ) )
				return cls.Create( index, owner, featureInfo );

			return null;
		}

		static protected Dictionary<string, Class> featureClasses = new Dictionary<string, Class>();

		static protected void Register( string typeAndRole, Class cls )
		{
			featureClasses.Add( typeAndRole, cls );
		}


		public int			  Index		  { get; set; }
		public string		  Type		  { get; private set; }
		public string		  Role		  { get; private set; }
		public virtual string Description { get { return null; } }


		public List<Function> Functions	   = new();

		private Entity		  owner;

		public FeatureInformationType FeatureInformation
		{
			get
			{
				FeatureInformationType info = new();

				info.description.featureAddress.device	= this.owner.Local.DeviceId;
				info.description.featureAddress.entity	= this.owner.Index;
				info.description.featureAddress.feature	= this.Index;
				info.description.featureType			= this.Type;
				info.description.role					= this.Role;
				info.description.description			= this.Description;

				List<SupportedFunctionType> functions = new();

				foreach ( Function function in this.Functions )
					functions.Add( function.SupportedFunction );

				if ( 0 < functions.Count )
					info.description.supportedFunction = functions.ToArray();

				return info;
			}
		}
	}
}
