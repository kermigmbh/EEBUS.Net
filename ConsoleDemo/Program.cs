using EEBUS;
using Microsoft.Extensions.Options;

namespace ConsoleDemo
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var settings = new Settings()
            {
                Device = new DeviceSettings()
                {
                    Name = "ConsoleDemoDevice",
                    Id = "Kermi-EEBUS-Demo-Client",
                    Model = "KermiDemo",
                    Brand = "Kermi",
                    Type = "EnergyManagementSystem",
                    Serial = "123456",
                    Port = 7200,
                    Entities = [
                        new EntitySettings { Type = "DeviceInformation" },
                        new EntitySettings { Type  = "CEM", UseCases = [
                            new UseCaseSettings {
                                Type = "limitationOfPowerConsumption",
                                Actor = "ControllableSystem",
                                InitLimits = new LimitSettings {
                                    Active = false,
                                    Limit = 4300,
                                    Duration = 7200,
                                    FailsafeLimit = 7200,
                                    NominalMax = 40000
                                }
                            }
                            ]}
                        ]

                },
                //Certificate = "XCenterEEBUS"
                Certificate = "EEBUS.net"
            };

            await new EebusDemo().RunAsync(settings);
        }
    }
}
