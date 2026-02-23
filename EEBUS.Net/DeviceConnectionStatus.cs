using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net
{
    public enum DeviceConnectionStatus
    {
        Unknown = 0,
        NotConnected = 1,
        HandshakeCompleted = 2,
        DiscoveryCompleted = 3,
        Connected = 4
    }
}
