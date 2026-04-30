using Makaretu.Dns;

namespace EEBUS
{
    public class MDNSService
    {
        private ServiceProfile _serviceProfile;
        private readonly ServiceDiscovery _sd;

        public MDNSService(ServiceDiscovery sd, ServiceProfile serviceProfile)
        {
            this._serviceProfile = serviceProfile;
            this._sd = sd;
        }


        public void AddProperty(string key, string value)
        {
            this._serviceProfile.AddProperty(key, value);
        }

        public void Run(CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                Thread.CurrentThread.IsBackground = true;

                // configure our EEBUS mDNS properties
                //AddProperty("name", localDevice.Name);
                //AddProperty("id", localDevice.DeviceId);
                //AddProperty("path", "/ship/");
                //AddProperty("register", "true");
                //AddProperty("ski", localDevice.SKI.ToString());
                //AddProperty("brand", localDevice.Brand);
                //AddProperty("type", localDevice.Type);
                //AddProperty("model", localDevice.Model);
                //AddProperty("serial", localDevice.Serial);

                try
                {
                    _sd.Advertise(this._serviceProfile);

                    await Task.Delay(-1, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    _sd.Unadvertise(this._serviceProfile);
                }
            });
        }
    }
}
