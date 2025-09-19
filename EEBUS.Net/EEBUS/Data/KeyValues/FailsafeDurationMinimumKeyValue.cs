using EEBUS.Models;
using EEBUS.SPINE.Commands;
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

		public override void SendEvent( Connection connection )
		{
			LPCorLPPEvents lpcOrLpp = connection.Local.GetUseCaseEvents<LPCorLPPEvents>();
			if ( null != lpcOrLpp )
				lpcOrLpp.DataUpdateFailsafeDurationMinimum( 0, XmlConvert.ToTimeSpan( this.Duration ) );
		}
	}
}
