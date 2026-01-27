using EEBUS.SPINE.Commands;

namespace EEBUS.Models
{
	public class LocalDevice : Device
	{
		public LocalDevice( byte[] ski, DeviceSettings settings )
			: base( settings.Id, ski )
		{			
			this.Name			   = settings.Name;
			this.Brand			   = settings.Brand;
			this.Type			   = settings.Type;
			this.Model			   = settings.Model;
			this.Serial			   = settings.Serial;
			this.NetworkFeatureSet = settings.NetworkFeatureSet;

			int index = 0;
			foreach ( EntitySettings entitySettings in settings.Entities )
			{
				this.Entities.Add( Entity.Create( index++, this, entitySettings ) );
			}

			this.settings = settings;
		}

		public string Brand				{ get; private set; }

		public string Type				{ get; private set; }

		public string Model				{ get; private set; }

		public string Serial			{ get; private set; }

		public string NetworkFeatureSet { get; private set; }


		private readonly DeviceSettings settings;
		
		public string ShipID
		{
			get
			{
				return "SHIP;SKI:" + this.SKI.ToString() + ",ID:" + this.Name + ";BRAND:" + this.Brand
					+ ";TYPE:" + this.Type + ";MODEL:" + this.Model + ";SERIAL:" + this.Serial + ";CAT:1;ENDSHIP;";
			}
		}

		public DeviceInformationType DeviceInformation
		{
			get
			{
				DeviceInformationType info = new();

				info.description.deviceAddress.device = this.DeviceId;
				info.description.deviceType			  = this.Type;
				info.description.networkFeatureSet	  = this.NetworkFeatureSet;

				return info;
			}
		}

		public EntityInformationType[] EntityInformations
		{
			get
			{
				List<EntityInformationType> infos = new();

				int index = 0;
				foreach ( Entity entity in this.Entities )
				{
					EntityInformationType info = new();

					info.description.entityAddress.device = this.DeviceId;
					info.description.entityAddress.entity = [index++];
					info.description.entityType			  = entity.Type;

					infos.Add( info );
				}

				return infos.ToArray();
			}
		}

		public DeviceSettings GetSettings()
		{
			return this.settings;
		}
	}
}
