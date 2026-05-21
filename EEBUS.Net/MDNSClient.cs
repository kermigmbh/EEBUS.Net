using System.Net.Sockets;

using Makaretu.Dns;

using EEBUS.Models;
using System.Diagnostics;
using EEBUS.Net.EEBUS.Models.Data;
using System.Text;
using System.Security.Cryptography;

namespace EEBUS
{
    public class MDNSClient
    {
        private Devices devices;
        private CancellationTokenSource? _cts;
        private readonly ServiceDiscovery _serviceDiscovery;

        private bool _serviceDiscoveryNeedsDispose = false;

        public MDNSClient(ServiceDiscovery? serviceDiscovery = null)
        {
            _serviceDiscoveryNeedsDispose = serviceDiscovery == null;
            this._serviceDiscovery = serviceDiscovery ?? new ServiceDiscovery();
        }

        public void Run(Devices devices)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            this.devices = devices;

            _ = Task.Run(() => RunInternalAsync(_cts.Token));
        }

        private async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            Thread.CurrentThread.IsBackground = true;

            //MulticastService mdns = new MulticastService();
            //ServiceDiscovery sd = new ServiceDiscovery();

            _serviceDiscovery.ServiceDiscovered += Sd_ServiceDiscovered;
            _serviceDiscovery.ServiceInstanceDiscovered += Sd_ServiceInstanceDiscovered;

            try
            {
                //mdns.Start();

                while (!cancellationToken.IsCancellationRequested)
                {
                    _serviceDiscovery.QueryAllServices();
                    //sd.QueryServiceInstances("_ship._tcp");
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
                try
                {
                    _serviceDiscovery.ServiceDiscovered -= Sd_ServiceDiscovered;
                    _serviceDiscovery.ServiceInstanceDiscovered -= Sd_ServiceInstanceDiscovered;
                }
                catch { }

                if (_serviceDiscoveryNeedsDispose)
                {
                    _serviceDiscovery.Dispose();
                }
                //sd.Dispose();
                //mdns.Stop();
            }
        }
        private void Sd_ServiceDiscovered(object? sender, DomainName e)
        {

            if (e?.ToString().StartsWith("_ship") == true)
            {
                _serviceDiscovery.Mdns.SendQuery(e);
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        private void Sd_ServiceInstanceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs ev)
        {
            string instanceName = ev.ServiceInstanceName.ToString();

            if (instanceName.Contains("._ship."))
            {
                Debug.WriteLine($"EEBUS service instance '{ev.ServiceInstanceName}' discovered.");
                ProcessShipRequest(ev.Message, instanceName);

            }
            else if (instanceName.Contains("._shippairing."))
            {
                Debug.WriteLine($"EEBUS service instance '{ev.ServiceInstanceName}' discovered.");
                ProcessShipPairingRequest(ev.Message, instanceName);
            }
        }

        private void ProcessShipRequest(Message mdnsMessage, string instanceName)
        {
            IEnumerable<SRVRecord> servers = mdnsMessage.AdditionalRecords.OfType<SRVRecord>();
            IEnumerable<AddressRecord> addresses = mdnsMessage.AdditionalRecords.OfType<AddressRecord>();
            IEnumerable<string>? txtRecords = mdnsMessage.AdditionalRecords.OfType<TXTRecord>()?.SelectMany(s => s.Strings);

            if (servers?.Count() > 0 && addresses?.Count() > 0 && txtRecords?.Count() > 0)
            {
                foreach (SRVRecord server in servers)
                {
                    IEnumerable<AddressRecord> serverAddresses = addresses.Where(w => w.Name == server.Target);
                    if (serverAddresses?.Count() > 0)
                    {
                        foreach (AddressRecord serverAddress in serverAddresses)
                        {
                            // we only want IPv4 addresses
                            if (serverAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                string id = string.Empty;
                                string path = string.Empty;
                                string ski = string.Empty;

                                foreach (string textRecord in txtRecords)
                                {
                                    if (textRecord.StartsWith("id"))
                                        id = textRecord.Substring(textRecord.IndexOf('=') + 1);

                                    if (textRecord.StartsWith("path"))
                                        path = textRecord.Substring(textRecord.IndexOf('=') + 1);

                                    if (textRecord.StartsWith("ski"))
                                        ski = textRecord.Substring(textRecord.IndexOf('=') + 1);

                                }

                                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(path))
                                {
                                    string url = serverAddress.Address.ToString() + ":" + server.Port.ToString() + path;
                                    RemoteDevice device = this.devices.GetOrCreateRemote(id, ski, url, instanceName);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ProcessShipPairingRequest(Message mdnsMessage, string instanceName)
        {
            IEnumerable<SRVRecord> servers = mdnsMessage.AdditionalRecords.OfType<SRVRecord>();
            IEnumerable<AddressRecord> addresses = mdnsMessage.AdditionalRecords.OfType<AddressRecord>();
            IEnumerable<string>? txtRecords = mdnsMessage.AdditionalRecords.OfType<TXTRecord>()?.SelectMany(s => s.Strings);

            if (servers?.Count() > 0 && addresses?.Count() > 0 && txtRecords?.Count() > 0)
            {
                foreach (SRVRecord server in servers)
                {
                    IEnumerable<AddressRecord> serverAddresses = addresses.Where(w => w.Name == server.Target);
                    if (serverAddresses?.Count() > 0)
                    {
                        foreach (AddressRecord serverAddress in serverAddresses)
                        {
                            // we only want IPv4 addresses
                            if (serverAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                string trustId = string.Empty;
                                string trustPar = string.Empty;
                                string alg = string.Empty;
                                string digest = string.Empty;
                                string trustNonce = string.Empty;

                                foreach (string textRecord in txtRecords)
                                {
                                    if (textRecord.StartsWith("trustId"))
                                        trustId = textRecord.Substring(textRecord.IndexOf('=') + 1);

                                    if (textRecord.StartsWith("trustPar"))
                                        trustPar = textRecord.Substring(textRecord.IndexOf('=') + 1);

                                    if (textRecord.StartsWith("alg"))
                                        alg = textRecord.Substring(textRecord.IndexOf('=') + 1);

                                    if (textRecord.StartsWith("digest"))
                                        digest = textRecord.Substring(textRecord.IndexOf('=') + 1);

                                    if (textRecord.StartsWith("trustNonce"))
                                        trustNonce = textRecord.Substring(textRecord.IndexOf('=') + 1);

                                }

                                PairedDevice? existing = this.devices.Paired.FirstOrDefault(p => p.Alg == alg && p.Digest == digest);
                                if (existing != null) return;   //device is already trusted

                                if (!ValidateDigest(alg, trustNonce, digest, txtRecords)) return;

                                if (!string.IsNullOrEmpty(trustId) && !string.IsNullOrEmpty(trustPar) && !string.IsNullOrEmpty(alg) && !string.IsNullOrEmpty(digest))
                                {
                                    this.devices.GetOrCreatePaired(trustPar, trustId, alg, digest);
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool ValidateDigest(string algorithm, string trustNonce, string digest, IEnumerable<string> txtRecords)
        {
            string secret = Convert.ToHexString(devices.Local.GetSecret());
            string key = secret + trustNonce;

            StringBuilder sb = new StringBuilder();
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("txtvers"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("parType"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("forId"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("forPar"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("trustId"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("trustPar"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("trustCurve"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("type"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("trustNonce"))};");
            sb.Append($"{txtRecords.FirstOrDefault(t => t.StartsWith("alg"))};");

            string message = sb.ToString();
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            byte[] keyBytes = Convert.FromHexString(key);

            using var hmac = new HMACSHA256(keyBytes);
            byte[] hash = hmac.ComputeHash(messageBytes);
            string calculatedDigest = Convert.ToHexString(hash);
            return calculatedDigest == digest;
        }
    }
}
