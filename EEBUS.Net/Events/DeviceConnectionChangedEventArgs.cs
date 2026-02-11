using EEBUS.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.Events
{
    public class DeviceConnectionChangedEventArgs : EventArgs
    {
        public DeviceConnectionChangeType ChangeType { get; set; }
        public required Connection Connection { get; set; }
    }


    public enum DeviceConnectionChangeType
    {
        Connected,
        Disconnected
    }
}
