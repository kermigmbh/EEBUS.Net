using EEBUS.KeyValues;
using EEBUS.SPINE.Commands;
using EEBUS.StateMachines;
using EEBUS.UseCases;

namespace EEBUS.Models
{
	public class LocalDevice : Device, IDisposable
	{
		private LimitStateMachine? _consumptionStateMachine;
		private LimitStateMachine? _productionStateMachine;
		private readonly object _stateMachineLock = new();

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

		/// <summary>
		/// Get the limit state machine for the specified power direction.
		/// Creates the state machine on first access.
		/// </summary>
		/// <param name="direction">PowerDirection.Consumption for LPC, PowerDirection.Production for LPP</param>
		/// <returns>The state machine for the given direction</returns>
		public LimitStateMachine GetStateMachine(PowerDirection direction)
		{
			lock (_stateMachineLock)
			{
				if (direction == PowerDirection.Consumption)
				{
					if (_consumptionStateMachine == null)
					{
						var failsafeLimit = GetFailsafeLimit(direction);
						_consumptionStateMachine = new LimitStateMachine(direction, failsafeLimit);
					}
					return _consumptionStateMachine;
				}
				else
				{
					if (_productionStateMachine == null)
					{
						var failsafeLimit = GetFailsafeLimit(direction);
						_productionStateMachine = new LimitStateMachine(direction, failsafeLimit);
					}
					return _productionStateMachine;
				}
			}
		}

		/// <summary>
		/// Get the limit state machine for the specified direction string ("consume" or "produce").
		/// Creates the state machine on first access.
		/// </summary>
		public LimitStateMachine GetStateMachine(string limitDirection)
		{
			var direction = limitDirection == "consume" ? PowerDirection.Consumption : PowerDirection.Production;
			return GetStateMachine(direction);
		}

		/// <summary>
		/// Get the current effective limit for the specified power direction.
		/// </summary>
		public EffectiveLimit GetEffectiveLimit(PowerDirection direction)
		{
			return GetStateMachine(direction).GetEffectiveLimit();
		}

		/// <summary>
		/// Get the current effective limit for the specified direction string.
		/// </summary>
		public EffectiveLimit GetEffectiveLimit(string limitDirection)
		{
			return GetStateMachine(limitDirection).GetEffectiveLimit();
		}

		/// <summary>
		/// Notify all state machines that a heartbeat was received.
		/// </summary>
		public void OnHeartbeatReceived()
		{
			lock (_stateMachineLock)
			{
				_consumptionStateMachine?.OnHeartbeatReceived();
				_productionStateMachine?.OnHeartbeatReceived();
			}
		}

		/// <summary>
		/// Get the failsafe limit value for a direction from KeyValues
		/// </summary>
		private long GetFailsafeLimit(PowerDirection direction)
		{
			if (direction == PowerDirection.Consumption)
			{
				var kv = this.KeyValues.FirstOrDefault(k => k.KeyName == "failsafeConsumptionActivePowerLimit");
				if (kv != null && kv is FailsafeConsumptionActivePowerLimitKeyValue fsKv)
				{
					return fsKv.Value;
				}
			}
			else
			{
				var kv = this.KeyValues.FirstOrDefault(k => k.KeyName == "failsafeProductionActivePowerLimit");
				if (kv != null && kv is FailsafeProductionActivePowerLimitKeyValue fsKv)
				{
					return fsKv.Value;
				}
			}
			return 0;
		}

		#region IDisposable

		private bool _disposed;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
				return;

			if (disposing)
			{
				lock (_stateMachineLock)
				{
					_consumptionStateMachine?.Dispose();
					_productionStateMachine?.Dispose();
				}
			}

			_disposed = true;
		}

		#endregion
	}
}
