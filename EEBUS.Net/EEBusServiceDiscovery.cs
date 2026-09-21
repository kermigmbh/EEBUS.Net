using System.Collections;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using Makaretu.Dns;

namespace EEBUS
{
    /// <summary>
    /// <see cref="ServiceDiscovery"/> whose underlying <see cref="MulticastService"/> ignores IPv4 link-local
    /// (169.254.0.0/16) addresses on interfaces that also have a routable IPv4 address.
    /// <para>
    /// The mDNS library joins the multicast group once per unicast address on a shared receiver socket. On Linux the
    /// second join for the same interface fails, so only the first enumerated address gets a sender. With an alias like
    /// <c>eth0:0</c> carrying a fixed link-local address this means queries are sent from the link-local address instead
    /// of the real subnet address.
    /// </para>
    /// </summary>
    public class EEBusServiceDiscovery : ServiceDiscovery
    {
        private readonly MulticastService _mdns;

        public EEBusServiceDiscovery()
            : this(new MulticastService(FilterNetworkInterfaces))
        {
        }

        private EEBusServiceDiscovery(MulticastService mdns)
            : base(mdns)
        {
            _mdns = mdns;
            _mdns.Start();
        }

        /// <summary>
        /// Wraps each interface so that IPv4 link-local addresses are hidden if a routable IPv4 address exists.
        /// </summary>
        public static IEnumerable<NetworkInterface> FilterNetworkInterfaces(IEnumerable<NetworkInterface> nics)
        {
            return nics.Select(nic => FilteredNetworkInterface.Wrap(nic));
        }

        /// <summary>
        /// Same as <see cref="MulticastService.GetLinkLocalAddresses"/> but with the same address filtering as the
        /// sockets, so advertised A records match the addresses queries are sent from.
        /// </summary>
        public static IEnumerable<IPAddress> GetLinkLocalAddresses()
        {
            return FilterNetworkInterfaces(MulticastService.GetNetworkInterfaces())
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork ||
                            (a.AddressFamily == AddressFamily.InterNetworkV6 && a.IsIPv6LinkLocal));
        }

        internal static bool IsIPv4LinkLocal(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
                return false;

            byte[] bytes = address.GetAddressBytes();
            return bytes[0] == 169 && bytes[1] == 254;  //link local address range as defined by RFC 3927
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _mdns.Dispose();
            }
        }

        private sealed class FilteredNetworkInterface : NetworkInterface
        {
            private readonly NetworkInterface _inner;
            private readonly UnicastIPAddressInformation[] _unicastAddresses;

            private FilteredNetworkInterface(NetworkInterface inner, UnicastIPAddressInformation[] unicastAddresses)
            {
                _inner = inner;
                _unicastAddresses = unicastAddresses;
            }

            public static NetworkInterface Wrap(NetworkInterface nic)
            {
                UnicastIPAddressInformation[] all;
                try
                {
                    all = nic.GetIPProperties().UnicastAddresses.ToArray();
                }
                catch
                {
                    return nic;
                }

                bool hasRoutableIPv4 = all.Any(u => u.Address.AddressFamily == AddressFamily.InterNetwork
                                                    && !IsIPv4LinkLocal(u.Address)
                                                    && !IPAddress.IsLoopback(u.Address));
                if (!hasRoutableIPv4)
                    return nic;

                UnicastIPAddressInformation[] filtered = all.Where(u => !IsIPv4LinkLocal(u.Address)).ToArray();
                if (filtered.Length == all.Length)
                    return nic;

                return new FilteredNetworkInterface(nic, filtered);
            }

            public override string Id => _inner.Id;
            public override string Name => _inner.Name;
            public override string Description => _inner.Description;
            public override OperationalStatus OperationalStatus => _inner.OperationalStatus;
            public override long Speed => _inner.Speed;
            public override bool IsReceiveOnly => _inner.IsReceiveOnly;
            public override bool SupportsMulticast => _inner.SupportsMulticast;
            public override NetworkInterfaceType NetworkInterfaceType => _inner.NetworkInterfaceType;
            public override IPInterfaceStatistics GetIPStatistics() => _inner.GetIPStatistics();
            public override IPv4InterfaceStatistics GetIPv4Statistics() => _inner.GetIPv4Statistics();
            public override PhysicalAddress GetPhysicalAddress() => _inner.GetPhysicalAddress();
            public override bool Supports(NetworkInterfaceComponent networkInterfaceComponent) => _inner.Supports(networkInterfaceComponent);

            public override IPInterfaceProperties GetIPProperties()
                => new FilteredIPInterfaceProperties(_inner.GetIPProperties(), _unicastAddresses);
        }

        private sealed class FilteredIPInterfaceProperties : IPInterfaceProperties
        {
            private readonly IPInterfaceProperties _inner;
            private readonly UnicastIPAddressInformationCollection _unicastAddresses;

            public FilteredIPInterfaceProperties(IPInterfaceProperties inner, UnicastIPAddressInformation[] unicastAddresses)
            {
                _inner = inner;
                _unicastAddresses = new FilteredUnicastIPAddressInformationCollection(unicastAddresses);
            }

            public override UnicastIPAddressInformationCollection UnicastAddresses => _unicastAddresses;
            public override bool IsDnsEnabled => _inner.IsDnsEnabled;
            public override string DnsSuffix => _inner.DnsSuffix;
            public override bool IsDynamicDnsEnabled => _inner.IsDynamicDnsEnabled;
            public override IPAddressInformationCollection AnycastAddresses => _inner.AnycastAddresses;
            public override MulticastIPAddressInformationCollection MulticastAddresses => _inner.MulticastAddresses;
            public override IPAddressCollection DnsAddresses => _inner.DnsAddresses;
            public override GatewayIPAddressInformationCollection GatewayAddresses => _inner.GatewayAddresses;
            public override IPAddressCollection DhcpServerAddresses => _inner.DhcpServerAddresses;
            public override IPAddressCollection WinsServersAddresses => _inner.WinsServersAddresses;
            public override IPv4InterfaceProperties GetIPv4Properties() => _inner.GetIPv4Properties();
            public override IPv6InterfaceProperties GetIPv6Properties() => _inner.GetIPv6Properties();
        }

        private sealed class FilteredUnicastIPAddressInformationCollection : UnicastIPAddressInformationCollection
        {
            private readonly UnicastIPAddressInformation[] _items;

            public FilteredUnicastIPAddressInformationCollection(UnicastIPAddressInformation[] items)
            {
                _items = items;
            }

            public override int Count => _items.Length;
            public override bool IsReadOnly => true;
            public override UnicastIPAddressInformation this[int index] => _items[index];
            public override bool Contains(UnicastIPAddressInformation address) => _items.Contains(address);
            public override void CopyTo(UnicastIPAddressInformation[] array, int offset) => _items.CopyTo(array, offset);
            public override IEnumerator<UnicastIPAddressInformation> GetEnumerator() => ((IEnumerable<UnicastIPAddressInformation>)_items).GetEnumerator();
        }
    }
}
