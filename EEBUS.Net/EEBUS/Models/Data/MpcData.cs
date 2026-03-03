using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Models.Data
{
    public class MpcData
    {
        public long? AcPowerTotal { get; set; }
        public AcPhaseData? AcPower {  get; set; }
        public long? AcEnergyConsumed { get; set; }
        public AcPhaseData? AcCurrent {  get; set; }
        public AcPhaseData? AcVoltage {  get; set; }
        public long? AcFrequency { get; set; }
    }
}
