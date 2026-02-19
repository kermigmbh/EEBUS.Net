using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace EEBUS.Net.Events
{
    public class NewConnectionValidationEventArgs : EventArgs
    {
        public required X509Certificate2 Certificate { get; set; }
        public required string Ski { get; set; }
        public required string RemoteEndpoint { get; set; }
    }
}
