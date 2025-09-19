using EEBUS.Messages;
using EEBUS.SPINE.Commands;
using EEBUS.UseCases;

namespace EEBUS.Models
{
	public class Device
	{
		public Device( string id, byte[] ski )
		{
			this.DeviceId  = id;
			this.SKI	   = new SKI( ski );
		}

		public Device( string id, string ski )
		{
			this.DeviceId  = id;
			this.SKI	   = new SKI( ski );
		}

		public string			   DeviceId		  { get; private set; }
								 
		public string			   Name			  { get; set; }
								 
		public SKI				   SKI			  { get; set; }
								 
		public string			   Error		  { get; set; }
								 
								 
		public List<Entity>		   Entities		  = new();
		public List<DataStructure> DataStructures = new();
		public List<KeyValue>	   KeyValues	  = new();
		public List<UseCaseEvents> UseCaseEvents  = new();

		public override bool Equals( object obj )
		{
			if ( (obj == null) || ! GetType().Equals( obj.GetType() ) )
				return false;

			Device org = (Device) obj;
			return this.SKI == org.SKI;
		}

		public override int GetHashCode()
		{
			return this.SKI.GetHashCode();
		}

		public int GetId( KeyValue keyValue )
		{
			return this.KeyValues.IndexOf( keyValue );
		}

		public T GetKeyValue<T>() where T : KeyValue
		{
			foreach ( KeyValue kv in this.KeyValues )
				if ( kv.GetType() == typeof( T ) )
					return kv as T;

			return null;
		}

		public void Add( DataStructure dataStructure )
		{
			dataStructure.Id = (uint) this.DataStructures.Count;
			this.DataStructures.Add( dataStructure );
		}

		public List<T> GetDataStructures<T>() where T : DataStructure
		{
			List<T> datas = new();

			foreach ( var data in this.DataStructures )
				if ( data is T )
					datas.Add( data as T );

			return datas;
		}

		public T GetDataStructure<T>( uint id ) where T : DataStructure
		{
			foreach ( var data in this.DataStructures )
				if ( data is T && data.Id == id )
					return data as T;

			return null;
		}

		public void Add( KeyValue keyValue )
		{
			this.KeyValues.Add( keyValue );
		}

		public void AddUnique( KeyValue keyValue )
		{
			if ( ! this.KeyValues.Any( kv => kv.KeyName == keyValue.KeyName ) )
				this.KeyValues.Add( keyValue );
		}

		public void FillData<T>( List<T> dataList, Connection connection )
		{
			this.Entities.ForEach( e => e.FillData( dataList, connection ) );
		}

		public void AddUseCaseEvents( UseCaseEvents eventsInterface )
		{
			this.UseCaseEvents.Add( eventsInterface );
		}

		public void RemoveUseCaseEvents( UseCaseEvents eventsInterface )
		{
			this.UseCaseEvents.Remove( eventsInterface );
		}

		public T GetUseCaseEvents<T>() where T: class, UseCaseEvents
		{
			return this.UseCaseEvents.FirstOrDefault( uce => uce.GetType().GetInterfaces().Any( i => i == typeof( T ) ) ) as T;
		}

		public void SetDiscoveryData( NodeManagementDetailedDiscoveryData payload, Connection connection )
		{
			if ( 1 != payload.cmd.Length )
				return;

			this.DeviceId = payload.cmd[0].nodeManagementDetailedDiscoveryData.deviceInformation.description.deviceAddress.device;
			FeatureInformationType[] featureInfos = payload.cmd[0].nodeManagementDetailedDiscoveryData.featureInformation;

			foreach ( EntityInformationType entityInfo in payload.cmd[0].nodeManagementDetailedDiscoveryData.entityInformation )
			{
				Entity entity = Entity.Create( connection.Local, entityInfo, featureInfos );

				if ( null != entity && ! this.Entities.Any( e => e.EqualIndex( entity.Index ) ) )
					this.Entities.Add( entity );
			}
		}

		public AddressType GetHeartbeatAddress( bool server )
		{
			string role = server ? "server" : "client";

			foreach( Entity entity in this.Entities )
			{
				Feature feature = entity.Features.Find( f => null != f && f.Type == "DeviceDiagnosis" && f.Role == role );
				if ( null != feature )
					return new AddressType() { device = this.DeviceId, entity = entity.Index, feature = feature.Index };
			}

			return null;
		}

		public AddressType GetElectricalConnectionAddress( bool source )
		{
			string role = source ? "server" : "client";

			foreach ( Entity entity in this.Entities )
			{
				Feature feature = entity.Features.Find( f => null != f && f.Type == "ElectricalConnection" && f.Role == role );
				if ( null != feature )
					return new AddressType() { device = this.DeviceId, entity = entity.Index, feature = feature.Index };
			}

			return null;
		}

		public AddressType GetMeasurementDataAddress( bool source )
		{
			string role = source ? "server" : "client";

			foreach ( Entity entity in this.Entities )
			{
				Feature feature = entity.Features.Find( f => null != f && f.Type == "Measurement" && f.Role == role );
				if ( null != feature )
					return new AddressType() { device = this.DeviceId, entity = entity.Index, feature = feature.Index };
			}

			return null;
		}
	}
}
