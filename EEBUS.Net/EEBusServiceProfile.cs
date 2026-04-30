using EEBUS.Models;
using Makaretu.Dns;
using System.Net;

namespace EEBUS
{
	public class EEBusServiceProfile : ServiceProfile
	{
		public EEBusServiceProfile( string hostName, DeviceSettings deviceSettings, SKI ski, DomainName serviceName, IEnumerable<IPAddress>? addresses = null )
			: base()
		{
			InstanceName = deviceSettings.Id;	//settings.Device.Id
			ServiceName  = serviceName;
			DomainName fullyQualifiedName = FullyQualifiedName;
			HostName = hostName;	//Dns.GetHostName()
			Resources.Add( new SRVRecord
			{
				Name   = fullyQualifiedName,
				Port   = deviceSettings.Port,	//settings.Device.Port
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

            AddProperty("name", deviceSettings.Name);
            AddProperty("id", deviceSettings.Id);
            AddProperty("path", "/ship/");
            AddProperty("register", "true");
            AddProperty("ski", ski.ToString());
            AddProperty("brand", deviceSettings.Brand);
            AddProperty("type", deviceSettings.Type);
            AddProperty("model", deviceSettings.Model);
            AddProperty("serial", deviceSettings.Serial);
        }
	}
}
