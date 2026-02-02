using EEBUS;
using EEBUS.Entities;
using EEBUS.Models;
using EEBUS.Net;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ConsoleDemo
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var settings = Options.Create<Settings>(new Settings()
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
                Certificate = "XCenterEEBUS"
            });

            await new EebusDemo().RunAsync(settings.Value);
        }
    }
}
