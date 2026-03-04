using EEBUS.SPINE.Commands;

namespace EEBUS.MeasurementData
{
    public class MeasurementData
    {
        public uint measurementId { get; set; }
        public ElectricalConnectionParameterDescriptionDataType electricalConnectionParameterDescriptionData { get; set; }
        public MeasurementDataType measurementDataType { get; set; }
        public MeasurementDescriptionDataType measurementDescriptionDataType { get; set; }

        public long? MeasuredValue
        {
            get => measurementDataType.value?.number;
        }

        public void UpdateValue(long? value)
        {
            if (value != null)
            {
                measurementDataType.value ??= new ScaledNumberType();
                measurementDataType.value.number = value;
            }
        }
    }
}
