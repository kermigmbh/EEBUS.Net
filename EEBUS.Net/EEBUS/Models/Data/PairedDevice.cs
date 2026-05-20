using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Models.Data
{
    public class PairedDevice
    {
        public string TrustPar {  get; private set; }
        public string TrustId { get; private set; }
        public string Alg {  get; private set; }
        public string Digest { get; private set; }

        public PairedDevice(string trustPar, string trustId, string alg, string digest)
        {
            TrustPar = trustPar;
            TrustId = trustId;
            Alg = alg;
            Digest = digest;
        }
    }
}
