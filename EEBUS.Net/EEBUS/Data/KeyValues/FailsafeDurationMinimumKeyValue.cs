using EEBUS.Models;
using EEBUS.SPINE.Commands;
using EEBUS.UseCases;
using EEBUS.UseCases.ControllableSystem;
using System.Xml;
using ValueType = EEBUS.SPINE.Commands.ValueType;

namespace EEBUS.KeyValues
{
	public class FailsafeDurationMinimumKeyValue : KeyValue
	{
		public FailsafeDurationMinimumKeyValue( Device device, string duration, bool changable )
			: base( device )
		{
			this.Duration  = duration;
			this.changable = changable;
		}

		public override string KeyName	{ get { return "failsafeDurationMinimum"; } }
		public override string Type		{ get { return "duration"; } }

		public string		   Duration	{ get; set; }
		private bool		   changable;
		public override DeviceConfigurationKeyValueDescriptionDataType DescriptionData
		{
			get
			{
				DeviceConfigurationKeyValueDescriptionDataType descriptionData = new();

				descriptionData.keyId	  = this.device.GetId( this );
				descriptionData.keyName   = this.KeyName;
				descriptionData.valueType = this.Type;

				return descriptionData;
			}
		}

		public override DeviceConfigurationKeyValueDataType Data
		{
			get
			{
				DeviceConfigurationKeyValueDataType data = new();

				data.keyId			   = this.device.GetId( this );
				data.value.duration	   = this.Duration;
				data.isValueChangeable = this.changable;

				return data;
			}
		}

		public override void SetValue( ValueType value )
		{
			this.Duration = value.duration;
		}

		public override async Task SendEventAsync( Connection connection )
		{
			// Update both state machines with new failsafe duration
			var duration = XmlConvert.ToTimeSpan(this.Duration);
			connection.Local.GetStateMachine(PowerDirection.Consumption).SetFailsafeDuration(duration);
			connection.Local.GetStateMachine(PowerDirection.Production).SetFailsafeDuration(duration);

			List<LPCorLPPEvents> lpcOrLppEvents = connection.Local.GetUseCaseEvents<LPCorLPPEvents>();
			foreach (var lpcOrLpp in lpcOrLppEvents)
			{
				await lpcOrLpp.DataUpdateFailsafeDurationMinimumAsync(0, duration, connection.Remote?.SKI.ToString() ?? string.Empty);
			}
		}
	}
}
