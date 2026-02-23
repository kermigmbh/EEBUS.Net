using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Models.Data
{
    public class LpcData
    {
        public bool LpcLimitActive { get; set; }
        public long LpcLimit { get; set; }
        public TimeSpan LpcLimitDuration {  get; set; }
        public long LpcFailsafeLimit {  get; set; }
        public TimeSpan FailsafeLimitDuration { get; set; }
    }
}
