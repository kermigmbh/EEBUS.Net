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
        private ServiceProfile serviceProfile;
        private readonly Settings settings;
        private readonly ServiceDiscovery _sd;

        private bool _serviceDiscoveryNeedsDispose = false;

        public MDNSService(IOptions<Settings> options, ServiceDiscovery? serviceDiscovery = null)
        {
            this.settings = options.Value;
            this.serviceProfile = new EEBusServiceProfile(Dns.GetHostName(), this.settings.Device.Id, "_ship._tcp", this.settings.Device.Port);

            _sd = serviceDiscovery ?? new ServiceDiscovery();
            _serviceDiscoveryNeedsDispose = serviceDiscovery == null;
        }

        public MDNSService(IConfigurationSection settings, ServiceDiscovery? serviceDiscovery = null)
        {
            this.settings = settings.Get<Settings>();
            this.serviceProfile = new EEBusServiceProfile(Dns.GetHostName(), this.settings.Device.Id, "_ship._tcp", this.settings.Device.Port);

            _sd = serviceDiscovery ?? new ServiceDiscovery();
            _serviceDiscoveryNeedsDispose = serviceDiscovery == null;
        }

        public MDNSService(string deviceId, ushort devicePort, ServiceDiscovery? serviceDiscovery = null)
        {
            this.serviceProfile = new EEBusServiceProfile(Dns.GetHostName(), deviceId, "_ship._tcp", devicePort);

            _serviceDiscoveryNeedsDispose = serviceDiscovery == null;
            _sd = serviceDiscovery ?? new ServiceDiscovery();
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

                try
                {
                    _sd.Advertise(this.serviceProfile);

                    await Task.Delay(-1, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
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
