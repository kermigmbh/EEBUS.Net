using EEBUS.Models;
using EEBUS.SPINE.Commands;
using EEBUS.UseCases.ControllableSystem;
using System.Xml;
using ValueType = EEBUS.SPINE.Commands.ValueType;

namespace EEBUS.KeyValues
{
	public class PvCurtailmentLimitFactorKeyValue : KeyValue
	{
		public PvCurtailmentLimitFactorKeyValue( Device device, long value, short scale, bool changable )
			: base( device )
		{
			this.Value	   = value;
			this.Scale	   = scale;
			this.changable = changable;
		}

		public override string KeyName	 { get { return "pvCurtailmentLimitFactor"; } }
		public override string Type		 { get { return "scaledNumber"; } }
		public string		   Unit		 { get { return "pct"; } }

		public long			   Value	 { get; set; }
		public short		   Scale	 { get; set; }

		private bool		   changable;
		public override DeviceConfigurationKeyValueDescriptionDataType DescriptionData
		{
			get
			{
				DeviceConfigurationKeyValueDescriptionDataType descriptionData = new();

				descriptionData.keyId	  = this.device.GetId( this );
				descriptionData.keyName	  = this.KeyName;
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
			this.Value = value.scaledNumber.number;
		}

		public override void SendEvent( Connection connection )
		{
		}
	}
}
