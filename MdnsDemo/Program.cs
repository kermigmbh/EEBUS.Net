using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using EEBUS;

using Makaretu.Dns;

// Usage:
//   MdnsDemo                 discovery with link-local filtering (new behavior)
//   MdnsDemo --nofilter      discovery with the plain library behavior (for comparison)
//   MdnsDemo --advertise     additionally advertise a _shiptest._tcp service
//
// Compare with e.g.:  tcpdump -ni eth0 udp port 5353

bool useFilter = !args.Contains("--nofilter", StringComparer.OrdinalIgnoreCase);
bool advertise = args.Contains("--advertise", StringComparer.OrdinalIgnoreCase);

Console.WriteLine($"mDNS demo  (filter: {(useFilter ? "on" : "off")}, advertise: {(advertise ? "on" : "off")})");
Console.WriteLine();

Console.WriteLine("Network interfaces as seen by the mDNS library:");
foreach (NetworkInterface nic in MulticastService.GetNetworkInterfaces())
{
    IEnumerable<IPAddress> raw = nic.GetIPProperties().UnicastAddresses.Select(u => u.Address);
    IEnumerable<IPAddress> filtered = EEBusServiceDiscovery.FilterNetworkInterfaces([nic]).Single()
        .GetIPProperties().UnicastAddresses.Select(u => u.Address);

    Console.WriteLine($"  {nic.Name} ({nic.NetworkInterfaceType}, {nic.OperationalStatus})");
    Console.WriteLine($"    raw:      {string.Join(", ", raw)}");
    Console.WriteLine($"    filtered: {string.Join(", ", filtered)}");
}
Console.WriteLine();

Console.WriteLine("Addresses that would be advertised in A/AAAA records:");
Console.WriteLine("  library:  " + string.Join(", ", MulticastService.GetLinkLocalAddresses()));
Console.WriteLine("  filtered: " + string.Join(", ", EEBusServiceDiscovery.GetLinkLocalAddresses()));
Console.WriteLine();

using ServiceDiscovery sd = EEBusServiceDiscovery.Create(useFilter);

sd.Mdns.NetworkInterfaceDiscovered += (_, e) =>
{
    foreach (NetworkInterface nic in e.NetworkInterfaces)
        Console.WriteLine($"[nic] using '{nic.Name}'");
};

sd.Mdns.AnswerReceived += (_, e) =>
{
    IPEndPoint? from = e.RemoteEndPoint;
    foreach (AddressRecord a in e.Message.Answers.OfType<AddressRecord>().Concat(e.Message.AdditionalRecords.OfType<AddressRecord>()))
    {
        if (a.Address.AddressFamily == AddressFamily.InterNetwork)
            Console.WriteLine($"[answer] {a.Name} -> {a.Address}  (from {from})");
    }
};

sd.ServiceInstanceDiscovered += (_, e) =>
{
    if (e.ServiceInstanceName.ToString().Contains("_ship._tcp", StringComparison.OrdinalIgnoreCase))
        Console.WriteLine($"[ship] instance discovered: {e.ServiceInstanceName}  (from {e.RemoteEndPoint})");
};

if (advertise)
{
    var profile = new EEBusServiceProfile(Dns.GetHostName(), "Kermi-EEBUS-Mdns-Demo", "_shiptest._tcp", 7200,
        EEBusServiceDiscovery.GetLinkLocalAddresses(useFilter));
    profile.AddProperty("txtvers", "1");
    profile.AddProperty("id", "Kermi-EEBUS-Mdns-Demo");
    profile.AddProperty("path", "/ship/");
    profile.AddProperty("register", "true");
    sd.Advertise(profile);
    Console.WriteLine($"Advertising {profile.FullyQualifiedName} on {string.Join(", ", profile.Resources.OfType<AddressRecord>().Select(r => r.Address))}");
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine("Querying _ship._tcp every 5s. Press Ctrl+C to stop.");
Console.WriteLine();

try
{
    while (!cts.IsCancellationRequested)
    {
        sd.QueryServiceInstances("_ship._tcp");
        await Task.Delay(5000, cts.Token);
    }
}
catch (OperationCanceledException)
{
}
