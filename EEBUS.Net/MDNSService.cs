using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using Makaretu.Dns;

using EEBUS.Models;

namespace EEBUS
{
	public class MDNSService
	{
		public MDNSService( IOptions<Settings> options )
		{
			this.settings = options.Value;

			this.serviceProfile = new EEBusServiceProfile( Dns.GetHostName(), this.settings.Device.Id, "_ship._tcp", this.settings.Device.Port );
		}

		public MDNSService( IConfigurationSection settings )
		{
			this.settings = settings.Get<Settings>();
			this.settings.Device.Port = 7134;

			this.serviceProfile = new EEBusServiceProfile( Dns.GetHostName(), this.settings.Device.Id, "_ship._tcp", this.settings.Device.Port );
		}

		private ServiceProfile    serviceProfile;
		private X509Certificate2  cert;
		private readonly Settings settings;

		public void AddProperty( string key, string value )
		{
			this.serviceProfile.AddProperty( key, value );
		}

		public X509Certificate2 Cert
		{
			get
			{
				return this.cert;
			}
		}

		public void Run( Devices devices )
		{
			_ = Task.Run( async() =>
			{
				Thread.CurrentThread.IsBackground = true;

				MulticastService mdns = new MulticastService();
				ServiceDiscovery sd   = new ServiceDiscovery( mdns );

				cert = CertificateGenerator.GenerateCert( this.settings.Certificate );

				byte[] hash = SHA1.Create().ComputeHash( cert.GetPublicKey() );

				LocalDevice localDevice = devices.GetOrCreateLocal( hash, settings );

				// configure our EEBUS mDNS properties
				AddProperty( "name",     localDevice.Name );
				AddProperty( "id",       localDevice.DeviceId );
				AddProperty( "path",     "/ship/" );
				AddProperty( "register", "true" );
				AddProperty( "ski",      localDevice.SKI.ToString() );
				AddProperty( "brand",    localDevice.Brand );
				AddProperty( "type",     localDevice.Type );
				AddProperty( "model",    localDevice.Model );
				AddProperty( "serial",   localDevice.Serial );

				try
				{
					mdns.Start();

					sd.Advertise( this.serviceProfile );

					await Task.Delay( -1 ).ConfigureAwait( false );
				}
				catch ( Exception ex )
				{
					Console.WriteLine( ex.Message );
				}
				finally
				{
					sd.Dispose();
					mdns.Stop();
				}
			} );
		}
	}
}
