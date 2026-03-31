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

        //public void UpdateValue(long? value)
        //{
        //    if (value != null && measurementDataType != null)
        //    {
        //        measurementDataType.value ??= new ScaledNumberType();
        //        measurementDataType.value.number = value;
        //    }
        //}

        public void UpdateValue(float? value)
        {
            if (value != null && measurementDataType != null)
            {
                short scale = 0;
                while (value % 1 != 0)  //As long as we have decimal places...
                {
                    value *= 10;    //...move the comma to the left...
                    scale--;    //...and decrease the scale
                }
                /*
                 * Example: 0.0013
                 * 1. loop: 0.0013 % 1 == 0.0013
                 *      value *= 10 == 0.013
                 *      scale-- == -1
                 * 2. loop: 0.013 % 1 == 0.013
                 *      value *= 10 == 0.13
                 *      scale-- == -2
                 * 3. loop: 0.13 % 1 == 0.13
                 *      value *= 10 == 1.3
                 *      scale-- == -3
                 * 4. loop: 1.3 % 1 == 0.3
                 *      value *= 10 == 13
                 *      scale-- == -4
                 * 5. loop: 13 % 1 == 0 -> done, scaledNumber = 13 * 10 ^ -4 = 0.0013
                 */

                long number = (long)value;  
                while (number % 10 == 0)    //As long as we have trailing zeroes...
                {
                    number /= 10;   //...divide by 10...
                    scale++;    //and increase scale
                }

                /*
                 * Example: 13000
                 * 1. loop: 13000 % 10 == 0
                 *      number /= 10 == 1300
                 *      scale++ = 1
                 * 2. loop: 1300 % 10 == 0
                 *      number /= 10 == 130
                 *      scale++ = 2
                 * 3. loop: 130 % 10 == 0
                 *      number /= 10 == 13
                 *      scale++ = 3
                 * 4. loop: 13 % 10 == 3 -> done, scaledNumber = 13 * 10 ^ 3 = 13000
                 */

                measurementDataType.value ??= new Models.ScaledNumberType();
                measurementDataType.value.number = number;
                measurementDataType.value.scale = scale;
            }
        }
    }
}
