using System.Net.Sockets;

using Makaretu.Dns;

using EEBUS.Models;

namespace EEBUS
{
	public class MDNSClient
	{
		private Devices devices;
		private CancellationTokenSource? _cts;

		public void Run( Devices devices)
		{
			_cts?.Cancel();
			_cts = new CancellationTokenSource();
            this.devices = devices;

            _ = Task.Run(() => RunInternalAsync(_cts.Token));
		}

		private async Task RunInternalAsync(CancellationToken cancellationToken)
		{
            Thread.CurrentThread.IsBackground = true;

            MulticastService mdns = new MulticastService();
            ServiceDiscovery sd = new ServiceDiscovery(mdns);

            sd.ServiceDiscovered += (s, serviceName) => { mdns.SendQuery(serviceName); };
            sd.ServiceInstanceDiscovered += Sd_ServiceInstanceDiscovered;

            try
            {
                mdns.Start();

                while (!cancellationToken.IsCancellationRequested)
                {
                    sd.QueryAllServices();
                    devices.GarbageCollect();

                    await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                sd.Dispose();
                mdns.Stop();
            }
        }

		public void Stop()
		{
			_cts?.Cancel(); 
		}

		private void Sd_ServiceInstanceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs ev )
		{
			if ( ev.ServiceInstanceName.ToString().Contains( "._ship." ) )
			{
				Console.WriteLine( $"EEBUS service instance '{ev.ServiceInstanceName}' discovered." );

				IEnumerable<SRVRecord>     servers    = ev.Message.AdditionalRecords.OfType<SRVRecord>();
				IEnumerable<AddressRecord> addresses  = ev.Message.AdditionalRecords.OfType<AddressRecord>();
				IEnumerable<string>?        txtRecords = ev.Message.AdditionalRecords.OfType<TXTRecord>()?.SelectMany( s => s.Strings );

				if ( servers?.Count() > 0 && addresses?.Count() > 0 && txtRecords?.Count() > 0 )
				{
					foreach ( SRVRecord server in servers )
					{
						IEnumerable<AddressRecord> serverAddresses = addresses.Where( w => w.Name == server.Target );
						if ( serverAddresses?.Count() > 0 )
						{
							foreach ( AddressRecord serverAddress in serverAddresses )
							{
								// we only want IPv4 addresses
								if ( serverAddress.Address.AddressFamily == AddressFamily.InterNetwork )
								{
									string id   = string.Empty;
									string path = string.Empty;
									string ski  = string.Empty;

									foreach ( string textRecord in txtRecords )
									{
										if ( textRecord.StartsWith( "id" ) )
											id = textRecord.Substring( textRecord.IndexOf( '=' ) + 1 );
									
										if ( textRecord.StartsWith( "path" ) )
											path = textRecord.Substring( textRecord.IndexOf( '=' ) + 1 );

										if ( textRecord.StartsWith( "ski" ) )
											ski = textRecord.Substring( textRecord.IndexOf( '=' ) + 1 );

									}

									if ( ! string.IsNullOrEmpty( id ) && ! string.IsNullOrEmpty( path ) )
									{
										string url = serverAddress.Address.ToString() + ":" + server.Port.ToString() + path;
										RemoteDevice device = this.devices.GetOrCreateRemote( id, ski, url, ev.ServiceInstanceName.ToString() );
									}
								}
							}
						}
					}
				}
			}
		}
	}
}
