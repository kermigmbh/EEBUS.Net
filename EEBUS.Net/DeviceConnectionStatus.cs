using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net
{
    public enum DeviceConnectionStatus
    {
        /// <summary>
        /// Unknown - not connected
        /// </summary>
        Unknown = 0,
        UseCaseDiscoveryCompleted = 1,
        NodeDiscoveryCompleted = 2,
    }
}
