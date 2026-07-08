using EEBUS.Models;
using EEBUS.SPINE.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.Extensions
{
    public static class FloatExtensions
    {
        public static ScaledNumberType ToScaledNumber(this float value)
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

            var scaledNumber = new Models.ScaledNumberType();
            scaledNumber.number = number;
            scaledNumber.scale = scale;
            return scaledNumber;
        }
    }
}
