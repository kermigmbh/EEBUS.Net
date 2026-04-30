using Makaretu.Dns;
using System.Diagnostics;

namespace EEBUS
{
    public class MDNSClient(ServiceDiscovery sd)
    {
        private CancellationTokenSource? _cts;
        public event EventHandler<ServiceInstanceDiscoveryEventArgs>? InstanceDiscovered;
        private List<string>? _supportedProtocols { get; set; }


        public void Run(List<string>? supportedProtocols = null)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _supportedProtocols = supportedProtocols;
            _ = Task.Run(() => RunInternalAsync(_cts.Token));
        }

        private async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            Thread.CurrentThread.IsBackground = true;

            sd.ServiceDiscovered += Sd_ServiceDiscovered;
            sd.ServiceInstanceDiscovered += Sd_ServiceInstanceDiscovered;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    sd.QueryAllServices();

                    await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                sd.ServiceDiscovered -= Sd_ServiceDiscovered;
                sd.ServiceInstanceDiscovered -= Sd_ServiceInstanceDiscovered;
            }
        }
        private void Sd_ServiceDiscovered(object? sender, DomainName e)
        {
            if (_supportedProtocols == null || _supportedProtocols.Any(sp => sp.StartsWith(e.ToString())))
            {
                sd.Mdns.SendQuery(e);
            }
        }

        public void Stop()
        {
            _supportedProtocols = null;
            _cts?.Cancel();
        }

        private void Sd_ServiceInstanceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs ev)
        {
            if (_supportedProtocols == null || _supportedProtocols.Any(sp => ev.ServiceInstanceName.ToString().Contains(sp)))
            {
                Debug.WriteLine($"Service instance '{ev.ServiceInstanceName}' discovered.");
                InstanceDiscovered?.Invoke(this, ev);
            }
        }
    }
}
