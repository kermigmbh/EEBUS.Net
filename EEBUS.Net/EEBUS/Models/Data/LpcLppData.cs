using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Models.Data
{
    public class LpcLppData
    {
        public bool? LimitActive { get; set; }
        public long? Limit { get; set; }
        public int? LimitDuration {  get; set; }
        public long? FailSafeLimit { get; set; }
        public long? ContractualNominalMax {  get; set; }

        public bool IsEmpty()
        {
            return
                LimitActive == null &&
                Limit == null &&
                LimitDuration == null &&
                FailSafeLimit == null &&
                ContractualNominalMax == null;
        }
    }
}
