
namespace EEBUS.Models
{
    [System.SerializableAttribute()]
    public class ScaledNumberType
    {
        public long number { get; set; }

        public short? scale { get; set; }

        /// <summary>
        /// Calculate the actual limit value from scaled number
        /// </summary>
        public long ToLong()
        {
            if (!scale.HasValue || scale == 0)
                return number;
            if (scale > 0)
                return number * (long)Math.Pow(10, scale.Value);
            return number / (long)Math.Pow(10, -scale.Value);
        }
    }
}