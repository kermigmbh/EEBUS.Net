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
        private static RemoteDevice? _remoteDevice;

        private static EEBUSManager? s_EebusManager;

        static async Task Main(string[] args)
        {
            var settings = Options.Create<Settings>(new Settings()
            {
                Device = new DeviceSettings()
                {
                    Name = "ConsoleDemoDevice",
                    Id = "Kermi-EEBUS-Demo-Adapter",
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

            foreach (string ns in new string[] {"EEBUS.SHIP.Messages", "EEBUS.SPINE.Commands", "EEBUS.Entities",
                                                 "EEBUS.UseCases.ControllableSystem", "EEBUS.UseCases.GridConnectionPoint",
                                                 "EEBUS.Features" })
            {
                foreach (Type type in GetTypesInNamespace(typeof(Settings).Assembly, ns))
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }

            s_EebusManager = new EEBUSManager(settings.Value);
            s_EebusManager.DeviceFound += Manager_DeviceFound;


            s_EebusManager.StartDeviceSearch();
            while (_remoteDevice == null) { }
            s_EebusManager.StopDeviceSearch();

            string? hostString = await s_EebusManager.ConnectAsync(_remoteDevice.SKI.ToString());
            if (hostString == null)
            {
                Console.WriteLine("Failed to connect to client!");
            } else
            {
                Console.WriteLine("Connection established!");

                while (true)
                {
                    Console.Write(":> ");
                    string? input = Console.ReadLine();
                    if (input == null) continue;
                    if (input == "exit") break;

                    Read(input);
                }
                await s_EebusManager.DisconnectAsync(new Microsoft.AspNetCore.Http.HostString(hostString));
            }
        }

        private static void Read(string address)
        {
            var local = s_EebusManager?.GetLocal();
            if (local == null)
            {
                Console.WriteLine("No local device found");
                return;
            }

            if (local.TryGetValue(address, StringComparison.OrdinalIgnoreCase, out JToken? value))
            {
                if (value.Type == JTokenType.Float)
                {
                    float floatValue = float.Parse(value.ToString());
                    Console.WriteLine($"Read value from address [{address}]: {floatValue}");
                }

                if (value.Type == JTokenType.Integer)
                {
                    int intValue = int.Parse(value.ToString());
                    Console.WriteLine($"Read value from address [{address}]: {intValue}");
                }

                if (value.Type == JTokenType.Boolean)
                {
                    bool boolValue = bool.Parse(value.ToString());
                    Console.WriteLine($"Read value from address [{address}]: {boolValue}");
                }
            }
        }

        private static void Manager_DeviceFound(object? sender, EEBUS.Models.RemoteDevice e)
        {
            if (_remoteDevice != null) return;

            if (e.Name.Contains("controlbox", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Found device! " + e.Name);
                _remoteDevice = e;
            }
        }

        private static Type[] GetTypesInNamespace(Assembly assembly, string nameSpace)
        {
            return assembly.GetTypes()
                            .Where(t => String.Equals(t.Namespace, nameSpace, StringComparison.Ordinal))
                            .ToArray();
        }
    }
}
