using EEBUS.SPINE.Commands;
using ValueType = EEBUS.SPINE.Commands.ValueType;

namespace EEBUS.Models
{
	public abstract class KeyValue
	{
		protected KeyValue( Device device )
		{
			this.device = device;
		}

		protected Device	   device;

		public abstract string KeyName { get; }
		public abstract string Type	   { get; }

		public abstract DeviceConfigurationKeyValueDescriptionDataType DescriptionData { get; }

		public abstract DeviceConfigurationKeyValueDataType			   Data			   { get; }

		public abstract Task SendEventAsync( Connection connection );

		public abstract void SetValue( ValueType value );
	}
}
