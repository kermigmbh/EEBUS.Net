using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Models.Data
{
    public class LppData
    {
        public bool LppLimitActive { get; set; }
        public long LppLimit { get; set; }
        public TimeSpan LppLimitDuration {  get; set; }
        public long LppFailSafeLimit { get; set; }
        public TimeSpan FailSafeLimitDuration {  get; set; }
    }
}
