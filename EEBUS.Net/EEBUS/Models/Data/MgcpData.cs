using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Models.Data
{
    public class MgcpData
    {
        public long? PvCurtailmentLimitFactor { get; set; }
        public long? AcPowerTotal { get; set; }
        public long? GridFeedIn {  get; set; }
        public long? GridConsumption {  get; set; }
        public AcPhaseData? AcCurrent {  get; set; }
        public AcPhaseData? AcVoltage {  get; set; }
        public long? AcFrequency { get; set; }
    }
}
