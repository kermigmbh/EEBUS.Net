using EEBUS.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net
{
    internal class StateChangedEventArgs
    {
        internal Connection.EState State { get; init; }
        internal RemoteDevice Remote { get; init; }

        internal StateChangedEventArgs(Connection.EState state, RemoteDevice remote)
        {
            State = state;
            Remote = remote;
        }
    }
}
