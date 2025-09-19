using EEBUS.SPINE.Commands;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EEBUS.Models
{
	public abstract class Entity
	{
		protected Entity( int index, LocalDevice local, EntitySettings entitySettings )
		{
			this.index = index;
			this.local = local;
			this.Type  = entitySettings.Type;

			foreach ( UseCaseSettings usecaseSettings in entitySettings.UseCases )
			{
				UseCases.Add( UseCase.Create( usecaseSettings, this ) );
			}
		}

		public Entity( int index, LocalDevice local, EntityInformationType entityInfo, FeatureInformationType[] featureInfos )
		{
			this.index = index;
			this.local = local;
			this.Type  = entityInfo.description.entityType;

			foreach ( FeatureInformationType featureInfo in featureInfos )
			{
				if ( EqualIndex( featureInfo.description.featureAddress.entity ) )
				{
					this.Features.Add( Feature.Create( featureInfo, this ) );
				}
			}
		}

		public abstract class Class
		{
			public abstract Entity Create( int index, LocalDevice local, EntitySettings entitySettings );

			public abstract Entity Create( int index, LocalDevice local, EntityInformationType entityInfo, FeatureInformationType[] featureInfos );
		}

		static public Entity Create( int index, LocalDevice local, EntitySettings entitySettings )
		{
			if ( entityClasses.TryGetValue( entitySettings.Type, out Class cls ) )
				return cls.Create( index, local, entitySettings );

			return null;
		}

		static public Entity Create( LocalDevice local, EntityInformationType entityInfo, FeatureInformationType[] featureInfos )
		{
			// Missing: if (1 < Index.Length) => this is a child of another entity. Look for it and set it as owner

			int[] Index = entityInfo.description.entityAddress.entity;
			int   index = Index[Index.Length - 1];

			string type = entityInfo.description.entityType;

			if ( entityClasses.TryGetValue( type, out Class cls ) )
				return cls.Create( index, local, entityInfo, featureInfos );

			return null;
		}

		static protected Dictionary<string, Class> entityClasses = new Dictionary<string, Class>();

		static protected void Register( string name, Class cls )
		{
			entityClasses.Add( name, cls );
		}


		private int			 index		{ get; }

		public string		 Type		{ get; }

		public List<Entity>  Entities = new();
		public List<Feature> Features = new();
		public List<UseCase> UseCases = new();

		private LocalDevice  local = null;
		private Entity		 owner = null;

		public int[] Index
		{
			get
			{
				if ( null == this.owner )
				{
					return new int[1] { this.index }; 
				}
				else
				{
					List<int> indices = this.owner.Index.ToList();
					indices.Add( this.index );
					return indices.ToArray();
				}
			}
		}

		public bool EqualIndex( int[] otherIndex )
		{
			if ( this.Index.Length != otherIndex.Length )
				return false;

			for ( int i = 0; i < this.Index.Length; i++ )
				if ( this.Index[i] != otherIndex[i] )
					return false;

			return true;
		}

		public LocalDevice Local
		{
			get
			{
				return this.local;
			}
		}

		public List<UseCaseInformationType> UseCaseInformations
		{
			get
			{
				List<string> actors = new();
				foreach ( var useCase in this.UseCases )
					if ( -1 == actors.IndexOf( useCase.Actor ) )
						actors.Add( useCase.Actor );

				List<UseCaseInformationType> infos = new();

				foreach ( string actor in actors )
				{
					UseCaseInformationType info = new();

					info.address.device	= this.Local.DeviceId;
					info.address.entity	= this.Index;
					info.actor			= actor;

					List<UseCaseSupportType> supports = new();

					foreach ( var useCase in this.UseCases )
						if ( useCase.Actor == actor )
							supports.Add( useCase.Information );

					info.useCaseSupport = supports.ToArray();

					infos.Add( info );
				}

				return infos;
			}
		}

		public void FillData<T>( List<T> dataList, Connection connection )
		{
			this.UseCases.ForEach( uc => uc.FillData( dataList, connection, this ) );
		}

		protected virtual int MinIndex { get { return 1; } }
		
		public Feature GetOrAdd( Feature feature )
		{
			Feature found = this.Features.FirstOrDefault( f => f.Type == feature.Type && f.Role == feature.Role );

			if ( null != found )
				return found;

			feature.Index = this.Features.Count + MinIndex;
			this.Features.Add( feature );

			return feature;
		}
	}
}
