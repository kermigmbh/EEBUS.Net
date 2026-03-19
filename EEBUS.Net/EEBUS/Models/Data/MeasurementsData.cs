using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Models.Data
{
    public class MeasurementsData
    {
        public float? AcPowerTotal { get; set; }
        public float? GridFeedIn { get; set; }
        public float? GridConsumption { get; set; }
        public AcPhaseData? AcPower { get; set; }
        public float? AcEnergyConsumed { get; set; }
        public AcPhaseData? AcCurrent { get; set; }
        public AcPhaseData? AcVoltage { get; set; }
        public float? AcFrequency { get; set; }
    }
}
