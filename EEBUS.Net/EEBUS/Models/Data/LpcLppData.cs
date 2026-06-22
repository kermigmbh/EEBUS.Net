using EEBUS.StateMachines;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Models.Data
{
    public class LpcLppData
    {
        public LimitState? LimitState { get; set; }
        public bool? LimitActive { get; set; }
        public long? Limit { get; set; }
        public int? LimitDuration { get; set; }
        public long? FailSafeLimit { get; set; }
        public long? ContractualNominalMax { get; set; }

        public bool IsEmpty()
        {
            return
                LimitState == null &&
                LimitActive == null &&
                Limit == null &&
                LimitDuration == null &&
                FailSafeLimit == null &&
                ContractualNominalMax == null;
        }
    }
}
