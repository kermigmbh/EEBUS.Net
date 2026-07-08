using EEBUS.Net.Extensions;
using EEBUS.SPINE.Commands;

namespace EEBUS.MeasurementData
{
    public class MeasurementData
    {
        public uint measurementId { get; set; }
        public ElectricalConnectionParameterDescriptionDataType? electricalConnectionParameterDescriptionData { get; set; }
        public MeasurementDataType? measurementDataType { get; set; }
        public MeasurementDescriptionDataType? measurementDescriptionDataType { get; set; }

        public float? MeasuredValue
        {
            get
            {
                long? number = measurementDataType?.value?.number;
                short? scale = measurementDataType?.value?.scale;

                if (number == null) return null;
                scale ??= 0;
                if (scale == 0) return number;

                return (float)(number * Math.Pow(10, (double)scale));
            }
        }

        public void UpdateValue(float? value)
        {
            if (value != null && measurementDataType != null)
            {
                measurementDataType.value = value.Value.ToScaledNumber();
            }
        }
    }
}
