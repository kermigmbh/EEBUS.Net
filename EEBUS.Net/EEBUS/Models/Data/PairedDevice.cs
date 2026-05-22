using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Models.Data
{
    public class PairedDevice
    {
        public string TrustPar {  get; private set; }
        public string TrustId { get; private set; }
        public ShipTrustType TrustType { get; set; }

        public PairedDevice(string trustPar, string trustId, ShipTrustType trustType = ShipTrustType.AddCu)
        {
            TrustPar = trustPar;
            TrustId = trustId;
            TrustType = trustType;
        }
    }
}
