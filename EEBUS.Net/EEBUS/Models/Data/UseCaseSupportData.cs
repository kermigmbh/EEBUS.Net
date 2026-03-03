using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Models.Data
{
    public class UseCaseSupportData
    {
        public UseCaseSupportDetailData? Lpc {  get; set; }
        public UseCaseSupportDetailData? Lpp {  get; set; }
        public UseCaseSupportDetailData? Mgcp {  get; set; }
        public UseCaseSupportDetailData? Mpc {  get; set; }
    }
}
