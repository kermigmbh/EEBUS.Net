using System.Net;

using Makaretu.Dns;

namespace EEBUS
{
	public class EEBusServiceProfile : ServiceProfile
	{
		public EEBusServiceProfile( string hostName, DomainName instanceName, DomainName serviceName, ushort port, IEnumerable<IPAddress>? addresses = null )
			: base()
		{
			InstanceName = instanceName;
			ServiceName  = serviceName;
			DomainName fullyQualifiedName = FullyQualifiedName;
			//DomainName domainName = new DomainName(ServiceName.ToString().Replace("._tcp", "").Replace("._udp", "")
			//	.Trim(new char[1] { '_' })
			//	.Replace("_", "-"));
			//HostName = DomainName.Join(InstanceName, domainName, Domain);
			HostName = hostName;
			Resources.Add( new SRVRecord
			{
				Name   = fullyQualifiedName,
				Port   = port,
				Target = HostName
			} );
			Resources.Add( new TXTRecord
			{
				Name	= fullyQualifiedName,
				Strings	= { "txtvers=1" }
			} );
			foreach ( IPAddress item in addresses ?? MulticastService.GetLinkLocalAddresses() )
			{
				Resources.Add( AddressRecord.Create( HostName, item ) );
			}
		}
	}
}
