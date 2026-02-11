using EEBUS.Models;
using EEBUS.SPINE.Commands;
using EEBUS.UseCases.ControllableSystem;
using ValueType = EEBUS.SPINE.Commands.ValueType;

namespace EEBUS.KeyValues
{
	public class FailsafeProductionActivePowerLimitKeyValue : KeyValue
	{
		public FailsafeProductionActivePowerLimitKeyValue( Device device, long value, short scale, bool changable )
			: base( device )
		{
			this.Value	   = value;
			this.Scale	   = scale;
			this.changable = changable;
		}

		public override string KeyName { get { return "failsafeProductionActivePowerLimit"; } }
		public override string Type	   { get { return "scaledNumber"; } }
		public string		   Unit	   { get { return "W"; } }

		public long			   Value   { get; set; }
		public short		   Scale   { get; set; }

		private bool		   changable;

		public override DeviceConfigurationKeyValueDescriptionDataType DescriptionData
		{
			get
			{
				DeviceConfigurationKeyValueDescriptionDataType descriptionData = new();

				descriptionData.keyId	  = this.device.GetId( this );
				descriptionData.keyName   = this.KeyName;
				descriptionData.valueType = this.Type;
				descriptionData.unit	  = this.Unit;

				return descriptionData;
			}
		}

		public override DeviceConfigurationKeyValueDataType Data
		{
			get
			{
				DeviceConfigurationKeyValueDataType data = new();

				data.keyId					   = this.device.GetId( this );
				data.value.scaledNumber		   = new();
				data.value.scaledNumber.number = this.Value;
				data.value.scaledNumber.scale  = this.Scale;
				data.isValueChangeable		   = this.changable;

				return data;
			}
		}

		public override void SetValue( ValueType value )
		{
			// value.scaledNumber.number is nullable, fall back to existing Value if null
			this.Value = value.scaledNumber?.number ?? this.Value;
		}

		public override async Task SendEventAsync( Connection connection )
		{
			List<LPPEvents> lppEvents = connection.Local.GetUseCaseEvents<LPPEvents>();
			foreach (var lpp in lppEvents)
			{
				await lpp.DataUpdateFailsafeProductionActivePowerLimitAsync(0, this.Value);
			}
		}
	}
}
