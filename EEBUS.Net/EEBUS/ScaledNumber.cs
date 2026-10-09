using System;
using System.Collections.Generic;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EEBUS.Net.EEBUS
{
    public static class ScaledNumber
    {
        public static long ToLong(long number, short scale)
        {
            if (scale == 0)
                return number;
            if (scale > 0)
                return number * (long)Math.Pow(10, scale);
            return number / (long)Math.Pow(10, -scale);
        }
    }
}
