using EEBUS.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.Events
{
    public class DeviceConnectionRequestEventArgs
    {
        public SKI Ski { get; init; }

        public DeviceConnectionRequestEventArgs(SKI ski)
        {
            Ski = ski;
        }
    }
}
