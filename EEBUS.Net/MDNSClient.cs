using System.Net.Sockets;

using Makaretu.Dns;

using EEBUS.Models;
using System.Diagnostics;
using EEBUS.Net.EEBUS.Models.Data;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace EEBUS
{
    public class MDNSClient
    {
        private Devices? devices;
        private CancellationTokenSource? _cts;
        private readonly ServiceDiscovery _serviceDiscovery;
        private Func<bool>? _allowShipPairingEvaluation;
        private List<(string Alg, string Digest)> _previousPairings = [];
        private object _lock = new object();

        private bool _serviceDiscoveryNeedsDispose = false;
        private readonly ILogger _logger;

        public MDNSClient(ServiceDiscovery? serviceDiscovery = null, Func<bool>? allowShipPairingEvaluation = null, ILogger? logger = null)
        {
            _serviceDiscoveryNeedsDispose = serviceDiscovery == null;
            this._serviceDiscovery = serviceDiscovery ?? new ServiceDiscovery();
            _allowShipPairingEvaluation = allowShipPairingEvaluation;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        }

        public void Run(Devices devices)
        {
            // Atomically swap in a new CTS, then cancel and dispose the old one.
            // Publish the new instance first so any racing reader of `_cts` sees a
            // live source instead of a disposed one.
            var previousCts = _cts;
            _cts = new CancellationTokenSource();
            if (previousCts != null)
            {
                try { previousCts.Cancel(); } catch (ObjectDisposedException) { }
                try { previousCts.Dispose(); } catch { }
            }

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
                    devices?.GarbageCollect();

                    await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MDNS] An error occurred in MDNSClient.");
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
            var cts = _cts;
            _cts = null;
            if (cts != null)
            {
                try { cts.Cancel(); } catch (ObjectDisposedException) { }
                try { cts.Dispose(); } catch { }
            }
        }

        private void Sd_ServiceInstanceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs ev)
        {
            string instanceName = ev.ServiceInstanceName.ToString();

            if (instanceName.Contains("._ship."))
            {
                _logger.LogTrace("[MDNS] EEBUS service instance '{instanceName}' discovered.", ev.ServiceInstanceName);
                ProcessShipRequest(ev.Message, instanceName);

            }
            else if (instanceName.Contains("._shippairing.") && _allowShipPairingEvaluation?.Invoke() == true)
            {
                _logger.LogTrace("[MDNS] EEBUS service instance '{instanceName}' discovered.", ev.ServiceInstanceName);
                ProcessShipPairingRequest(ev.Message, instanceName);
            }
        }

        private void ProcessShipRequest(Message mdnsMessage, string instanceName)
        {
            //This is not enough! Some devices send all of their records in the AdditionalRecords section, some send them in the Answers section, and some send them in both. So we need to check both sections for the records we need.
            //IEnumerable<SRVRecord> servers = mdnsMessage.AdditionalRecords.OfType<SRVRecord>();
            //IEnumerable<AddressRecord> addresses = mdnsMessage.AdditionalRecords.OfType<AddressRecord>();
            //IEnumerable<string>? txtRecords = mdnsMessage.AdditionalRecords.OfType<TXTRecord>()?.SelectMany(s => s.Strings);

            IEnumerable<SRVRecord> srvRecords = mdnsMessage.Answers.OfType<SRVRecord>().Concat(mdnsMessage.AdditionalRecords.OfType<SRVRecord>());
            IEnumerable<AddressRecord> addressRecords = mdnsMessage.Answers.OfType<AddressRecord>().Concat(mdnsMessage.AdditionalRecords.OfType<AddressRecord>());
            IEnumerable<TXTRecord> txtRecords = mdnsMessage.Answers.OfType<TXTRecord>().Concat(mdnsMessage.AdditionalRecords.OfType<TXTRecord>());
            IEnumerable<string> txtRecordStrings = txtRecords.SelectMany(s => s.Strings);
            
            if (srvRecords.Any() && addressRecords.Any() && txtRecordStrings.Any())
            {
                foreach (SRVRecord server in srvRecords)
                {
                    IEnumerable<AddressRecord> serverAddresses = addressRecords.Where(w => w.Name == server.Target);
                    if (serverAddresses.Any())
                    {
                        foreach (AddressRecord serverAddress in serverAddresses)
                        {
                            // we only want IPv4 addresses
                            if (serverAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                string id = string.Empty;
                                string path = string.Empty;
                                string ski = string.Empty;

                                foreach (string textRecord in txtRecordStrings)
                                {
                                    if (textRecord.StartsWith("id"))
                                        id = GetTxtRecordValue(textRecord);

                                    if (textRecord.StartsWith("path"))
                                        path = GetTxtRecordValue(textRecord);

                                    if (textRecord.StartsWith("ski"))
                                        ski = GetTxtRecordValue(textRecord);

                                }

                                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(ski))
                                {
                                    string url = serverAddress.Address.ToString() + ":" + server.Port.ToString() + path;
                                    this.devices?.GetOrCreateRemote(id, ski, url, instanceName);
                                }
                                else
                                {
                                    _logger.LogWarning("[MDNS] EEBUS service instance '{instanceName}' discovered but missing required TXT records.", instanceName);
                                }
                            }
                        }
                    } else
                    {
                        _logger.LogWarning("[MDNS] No server addresses found for EEBUS service instance '{instanceName}'.", instanceName);
                    }
                }
            } else
            {
                _logger.LogWarning("[MDNS] EEBUS service instance '{instanceName}' discovered but missing required records.", instanceName);
            }
        }

        private void ProcessShipPairingRequest(Message mdnsMessage, string instanceName)
        {
            //This is not enough! Some devices send all of their records in the AdditionalRecords section, some send them in the Answers section, and some send them in both. So we need to check both sections for the records we need.
            //IEnumerable<SRVRecord> servers = mdnsMessage.AdditionalRecords.OfType<SRVRecord>();
            //IEnumerable<AddressRecord> addresses = mdnsMessage.AdditionalRecords.OfType<AddressRecord>();
            //IEnumerable<string>? txtRecords = mdnsMessage.AdditionalRecords.OfType<TXTRecord>()?.SelectMany(s => s.Strings);

            IEnumerable<SRVRecord> srvRecords = mdnsMessage.Answers.OfType<SRVRecord>().Concat(mdnsMessage.AdditionalRecords.OfType<SRVRecord>());
            IEnumerable<AddressRecord> addressRecords = mdnsMessage.Answers.OfType<AddressRecord>().Concat(mdnsMessage.AdditionalRecords.OfType<AddressRecord>());
            IEnumerable<TXTRecord> txtRecords = mdnsMessage.Answers.OfType<TXTRecord>().Concat(mdnsMessage.AdditionalRecords.OfType<TXTRecord>());
            IEnumerable<string> txtRecordStrings = txtRecords.SelectMany(s => s.Strings);


            if (srvRecords.Any() && addressRecords.Any() && txtRecordStrings.Any())
            {
                if (!ValidateShipPairingRecords(txtRecordStrings)) return;

                foreach (SRVRecord server in srvRecords)
                {
                    IEnumerable<AddressRecord> serverAddresses = addressRecords.Where(w => w.Name == server.Target);
                    if (serverAddresses.Any())
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

                                foreach (string textRecord in txtRecordStrings)
                                {
                                    if (textRecord.StartsWith("trustId"))
                                        trustId = GetTxtRecordValue(textRecord);

                                    if (textRecord.StartsWith("trustPar"))
                                        trustPar = GetTxtRecordValue(textRecord);

                                    if (textRecord.StartsWith("alg"))
                                        alg = GetTxtRecordValue(textRecord);

                                    if (textRecord.StartsWith("digest"))
                                        digest = GetTxtRecordValue(textRecord);

                                    if (textRecord.StartsWith("trustNonce"))
                                        trustNonce = GetTxtRecordValue(textRecord);

                                }

                                bool alreadyPaired = false;
                                lock (_lock)
                                {
                                    //PairedDevice? existing = this.devices?.Paired.FirstOrDefault(p => p.Alg == alg && p.Digest == digest);
                                   alreadyPaired = _previousPairings.Any(p => p.Alg == alg && p.Digest == digest);
                                }
                                if (alreadyPaired) continue;

                                if (!ValidateDigest(alg, trustNonce, digest, txtRecordStrings)) continue;

                                if (!string.IsNullOrEmpty(trustId) && !string.IsNullOrEmpty(trustPar) && !string.IsNullOrEmpty(alg) && !string.IsNullOrEmpty(digest))
                                {
                                    lock(_lock)
                                    {
                                        _previousPairings.Add((alg, digest));
                                    }
                                    this.devices?.GetOrCreatePaired(trustPar, trustId);
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool ValidateShipPairingRecords(IEnumerable<string> txtRecords)
        {
            if (txtRecords.First() != "txtvers=1") return false;

            string forId = GetTxtRecordValue("forId", txtRecords);
            if (forId != devices?.Local.DeviceId) return false;

            string trustCurve = GetTxtRecordValue("trustCurve", txtRecords);
            if (trustCurve != "secp256r1") return false;

            string type = GetTxtRecordValue("type", txtRecords);
            if (type != "addCu") return false;

            string alg = GetTxtRecordValue("alg", txtRecords);
            if (alg != "hmachSha256") return false;

            string parType = GetTxtRecordValue("parType", txtRecords);
            if (parType != "fpSha256") return false;

            //Check for duplicate keys
            foreach (var record in txtRecords)
            {
                string key = GetTxtRecordKey(record);
                var foundKeys = txtRecords.Where(r => GetTxtRecordKey(r) == key);
                if (foundKeys.Count() > 1) return false;
            }

            return true;
        }

        private bool ValidateDigest(string algorithm, string trustNonce, string digest, IEnumerable<string> txtRecords)
        {
            string secret = Convert.ToHexString(devices?.Local.GetSecret() ?? []);
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

        private string GetTxtRecordValue(string txtRecord)
        {
            string[] kvp = txtRecord.Split("=");
            if (kvp.Length < 2) return string.Empty;

            return kvp[1];
        }

        private string GetTxtRecordKey(string txtRecord)
        {
            string[] kvp = txtRecord.Split("=");
            if (kvp.Length < 1) return string.Empty;

            return kvp[0];
        }

        private string GetTxtRecordValue(string key, IEnumerable<string> txtRecords)
        {
            string? txtRecord = txtRecords.FirstOrDefault(r => r.StartsWith(key));
            if (string.IsNullOrEmpty(txtRecord)) return string.Empty;

            return GetTxtRecordValue(txtRecord);
        }
    }
}
