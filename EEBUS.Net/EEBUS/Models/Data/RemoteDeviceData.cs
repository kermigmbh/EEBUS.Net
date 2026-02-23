using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Models.Data
{
    public class RemoteDeviceData
    {
        public string Ski {  get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public bool SupportsLpc { get; set; }
        public bool SupportsLpp { get; set; }
    }
}
