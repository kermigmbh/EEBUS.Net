using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using Makaretu.Dns;

using EEBUS.Models;

namespace EEBUS
{
    /// <summary>
    /// Broadcasts the ship service over mdns.
    /// </summary>
    public class MDNSService
    {
        /// <summary>
        /// Interval in which the multicast sockets of the mDNS library are recreated.
        /// <para>
        /// Background: in the field devices stopped being visible via mDNS after running for a long time, without any
        /// exception or log entry. Cause: the UDP receive loop inside <see cref="MulticastService"/> terminates silently
        /// on any socket error (e.g. ICMP port unreachable on Windows, transient NIC errors on Linux). The object stays
        /// alive and the advertised records are still in the catalog, but no query ever reaches the responder again, so
        /// nothing on our side can notice it. The library only recovers on a <c>NetworkAddressChanged</c> event.
        /// </para>
        /// <para>
        /// Fix: periodically call <see cref="IMulticastService.Start"/>. This recreates the sockets (same code path the
        /// library runs on an address change) but keeps all event handlers, so <see cref="ServiceDiscovery"/> keeps
        /// answering and <see cref="MDNSClient"/>, which shares the same instance, keeps receiving. Never use
        /// <see cref="IMulticastService.Stop"/> for this, it clears all event handlers and would break both permanently.
        /// <see cref="ServiceDiscovery.Advertise"/> only registers records in memory and does not need to be repeated.
        /// </para>
        /// </summary>
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

        private ServiceProfile serviceProfile;
        private readonly Settings settings;
        private readonly ServiceDiscovery _sd;

        private bool _serviceDiscoveryNeedsDispose = false;

        public MDNSService(IOptions<Settings> options, ServiceDiscovery? serviceDiscovery = null)
        {
            this.settings = options.Value;
            this.serviceProfile = new EEBusServiceProfile(Dns.GetHostName(), this.settings.Device.Id, "_ship._tcp", this.settings.Device.Port,
                EEBusServiceDiscovery.GetLinkLocalAddresses(this.settings.FilterLinkLocalAddresses));

            _sd = serviceDiscovery ?? EEBusServiceDiscovery.Create(this.settings.FilterLinkLocalAddresses);
            _serviceDiscoveryNeedsDispose = serviceDiscovery == null;
        }

        public MDNSService(IConfigurationSection settings, ServiceDiscovery? serviceDiscovery = null)
        {
            this.settings = settings.Get<Settings>();
            this.serviceProfile = new EEBusServiceProfile(Dns.GetHostName(), this.settings.Device.Id, "_ship._tcp", this.settings.Device.Port,
                EEBusServiceDiscovery.GetLinkLocalAddresses(this.settings.FilterLinkLocalAddresses));

            _sd = serviceDiscovery ?? EEBusServiceDiscovery.Create(this.settings.FilterLinkLocalAddresses);
            _serviceDiscoveryNeedsDispose = serviceDiscovery == null;
        }

        public MDNSService(string deviceId, ushort devicePort, ServiceDiscovery? serviceDiscovery = null, bool filterLinkLocalAddresses = true)
        {
            this.serviceProfile = new EEBusServiceProfile(Dns.GetHostName(), deviceId, "_ship._tcp", devicePort,
                EEBusServiceDiscovery.GetLinkLocalAddresses(filterLinkLocalAddresses));

            _serviceDiscoveryNeedsDispose = serviceDiscovery == null;
            _sd = serviceDiscovery ?? EEBusServiceDiscovery.Create(filterLinkLocalAddresses);
        }

        public void AddProperty(string key, string value)
        {
            this.serviceProfile.AddProperty(key, value);
        }

        public void Run(LocalDevice localDevice, CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                Thread.CurrentThread.IsBackground = true;

                // configure our EEBUS mDNS properties
                AddProperty("name", localDevice.Name);
                AddProperty("id", localDevice.DeviceId);
                AddProperty("path", "/ship/");
                AddProperty("register", "true");
                AddProperty("ski", localDevice.SKI.ToString());
                AddProperty("brand", localDevice.Brand);
                AddProperty("type", localDevice.Type);
                AddProperty("model", localDevice.Model);
                AddProperty("serial", localDevice.Serial);

                bool advertised = false;

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            if (!advertised)
                            {
                                _sd.Advertise(this.serviceProfile);
                                advertised = true;
                            }
                            else
                            {
                                // Recreates the sockets in case the receive loop died silently, see RefreshInterval.
                                // Start() keeps the event handlers, Stop() would clear them.
                                _sd.Mdns.Start();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"{DateTime.UtcNow} - mDNS {(advertised ? "refresh" : "advertise")} failed: {ex}");
                        }

                        await Task.Delay(RefreshInterval, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.UtcNow} - {ex.ToString()}");
                }
                finally
                {
                    try
                    {
                        _sd.Unadvertise(this.serviceProfile);
                    }
                    catch { }	//It can be that _sd is already disposed at this point

                    if (_serviceDiscoveryNeedsDispose)
                    {
                        _sd.Dispose();
                    }
                }
            });
        }
    }
}
