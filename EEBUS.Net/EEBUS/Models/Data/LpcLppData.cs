using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Models.Data
{
    public class LpcLppData
    {
        public bool LimitActive { get; set; }
        public long Limit { get; set; }
        public TimeSpan LimitDuration {  get; set; }
        public long FailSafeLimit { get; set; }
        
    }
}
