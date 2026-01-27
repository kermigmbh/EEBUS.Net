using EEBUS;
using EEBUS.Net;
using Microsoft.Extensions.Options;

namespace ConsoleDemo
{
    internal class Program
    {
        static void Main(string[] args)
        {

            MDNSClient mDNSClient = new MDNSClient();
            MDNSService mDNSService = new MDNSService(Options.Create<Settings>(new Settings()
            {
                Device = new DeviceSettings()
                {
                    Name = "ConsoleDemoDevice",
                    Id = "1",
                    Model = "DemoModel",
                     Serial = "123456",
                      Port = 7200
                     
                },

            }));
            var manager = new EEBUSManager(mDNSClient, mDNSService);
        }
    }
}
