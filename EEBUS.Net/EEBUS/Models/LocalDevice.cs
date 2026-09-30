using EEBUS.KeyValues;
using EEBUS.SPINE.Commands;
using EEBUS.StateMachines;
using EEBUS.UseCases;
using System.Security.Cryptography;

namespace EEBUS.Models
{
	public class LocalDevice : Device
	{
		private byte[]? _secret;
		private string? _fingerprint;

		public LocalDevice(byte[] ski, DeviceSettings settings, string? fingerprint = null)
			: base(settings.Id, ski)
		{
			this.Name = settings.Name;
			this.Brand = settings.Brand;
			this.Type = settings.Type;
			this.Model = settings.Model;
			this.Serial = settings.Serial;
			this.NetworkFeatureSet = settings.NetworkFeatureSet;

			int index = 0;
			foreach (EntitySettings entitySettings in settings.Entities)
			{
				var entity = Entity.Create(index++, this, entitySettings);
				if (entity != null)
				{
					this.Entities.Add(entity);
				}
			}

			this.settings = settings;

			if (!string.IsNullOrEmpty(settings.Secret) && settings.Secret.Length <= 32)
			{
				_secret = Convert.FromHexString(settings.Secret);
			}
			_fingerprint = fingerprint;
        }

		public string Brand { get; private set; }

		public string Type { get; private set; }

		public string Model { get; private set; }

		public string Serial { get; private set; }

		public string NetworkFeatureSet { get; private set; }


		private readonly DeviceSettings settings;

		public string ShipID
		{
			get
			{
				string fingerprintString = string.Empty;
				if (!string.IsNullOrEmpty(_fingerprint))
				{
					fingerprintString = $";FPH256:{_fingerprint}";
				}
				return "SHIP;SKI:" + this.SKI.ToString() + ";ID:" + this.Name + ";BRAND:" + this.Brand
					+ ";TYPE:" + this.Type + ";MODEL:" + this.Model + ";SERIAL:" + this.Serial + ";CAT:1" + fingerprintString + ";SPSEC:" + Convert.ToHexString(GetSecret()) + ";ENDSHIP;";
			}
		}

		public DeviceInformationType DeviceInformation
		{
			get
			{
				DeviceInformationType info = new();

				info.description.deviceAddress.device = this.DeviceId;
				info.description.deviceType = this.Type;
				info.description.networkFeatureSet = this.NetworkFeatureSet;

				return info;
			}
		}

		public EntityInformationType[] EntityInformations
		{
			get
			{
				List<EntityInformationType> infos = new();

				int index = 0;
				foreach (Entity entity in this.Entities)
				{
					EntityInformationType info = new();

					info.description.entityAddress.device = this.DeviceId;
					info.description.entityAddress.entity = [index++];
					info.description.entityType = entity.Type;

					infos.Add(info);
				}

				return infos.ToArray();
			}
		}

		public DeviceSettings GetSettings()
		{
			return this.settings;
		}

		public byte[] GetSecret()
		{
			if (_secret == null)
			{
				_secret = RandomNumberGenerator.GetBytes(16);
			}
			return _secret;
		}

		/// <summary>
		/// Get the failsafe limit value for a direction from KeyValues
		/// </summary>
		public long GetFailsafeLimit(PowerDirection direction)
		{
			if (direction == PowerDirection.Consumption)
			{
				FailsafeConsumptionActivePowerLimitKeyValue? lpcFailsafeLimitKeyValue = GetKeyValue<FailsafeConsumptionActivePowerLimitKeyValue>();
				if (null != lpcFailsafeLimitKeyValue)
					return lpcFailsafeLimitKeyValue.Value;
			}
			else
			{
				FailsafeProductionActivePowerLimitKeyValue? lppFailsafeLimitKeyValue = GetKeyValue<FailsafeProductionActivePowerLimitKeyValue>();
				if (null != lppFailsafeLimitKeyValue)
					return lppFailsafeLimitKeyValue.Value;
			}
			return 0;
		}

		/// <summary>
		/// Get the minimum amount of time the system is required to stay in the failsafe state from KeyValues
		/// </summary>
		public TimeSpan GetFailsafeDurationMinimum()
		{
			FailsafeDurationMinimumKeyValue? failsafeDurationKeyValue = GetKeyValue<FailsafeDurationMinimumKeyValue>();
			if (failsafeDurationKeyValue != null)
				return System.Xml.XmlConvert.ToTimeSpan(failsafeDurationKeyValue.Duration);

			return TimeSpan.FromHours(2);
		}
	}
}