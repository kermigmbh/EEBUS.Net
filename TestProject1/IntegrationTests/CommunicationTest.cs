using EEBUS;
using EEBUS.Models;
using EEBUS.Net;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace TestProject1.IntegrationTests
{
    public class CommunicationTest
    {


        [Fact]
        public async Task TwoEEBUSDevicesConnectivityAsync()
        {
            var settings1 = new Settings()
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
                                    Limit = 0,
                                    Duration = Timeout.InfiniteTimeSpan,
                                    FailsafeLimit = 7200,
                                    FailsafeDurationMinimum = TimeSpan.FromHours(2),
                                    NominalMax = 40000
                                },
                            },
                            new UseCaseSettings {
                                Type = "monitoringOfGridConnectionPoint",
                                Actor = "MonitoringAppliance"
                            },
                            new UseCaseSettings {
                                Type = "monitoringOfPowerConsumption",
                                Actor = "MonitoringAppliance"
                            },
                            new UseCaseSettings {
                                Type = "monitoringOfPowerConsumption",
                                Actor = "MonitoredUnit"
                            }
                            ]}
                           ]
                },
                //Certificate = "XCenterEEBUS"
                Certificate = "EEBUS1.net"
            };

            var settings2 = new Settings()
            {
                Device = new DeviceSettings()
                {
                    Name = "ConsoleDemoDevice",
                    Id = "Kermi-EEBUS-Demo-Client-2",
                    Model = "KermiDemo",
                    Brand = "Kermi",
                    Type = "EnergyManagementSystem",
                    Serial = "444444",
                    Port = 7201,
                    Entities = [
                      new EntitySettings { Type = "DeviceInformation" },
                        new EntitySettings { Type  = "CEM", UseCases = [
                            new UseCaseSettings {
                                Type = "limitationOfPowerConsumption",
                                Actor = "ControllableSystem",
                                InitLimits = new LimitSettings {
                                    Active = false,
                                    Limit = 0,
                                    Duration = Timeout.InfiniteTimeSpan,
                                    FailsafeLimit = 7200,
                                    FailsafeDurationMinimum = TimeSpan.FromHours(2),
                                    NominalMax = 40000
                                },
                            },
                            new UseCaseSettings {
                                Type = "monitoringOfGridConnectionPoint",
                                Actor = "MonitoringAppliance"
                            },
                            new UseCaseSettings {
                                Type = "monitoringOfPowerConsumption",
                                Actor = "MonitoringAppliance"
                            },
                            new UseCaseSettings {
                                Type = "monitoringOfPowerConsumption",
                                Actor = "MonitoredUnit"
                            }
                            ]}
                          ]
                },
                //Certificate = "XCenterEEBUS"
                Certificate = "EEBUS2.net"
            };
            EEBUSManager manager1 = new EEBUSManager(settings1);

            EEBUSManager manager2 = new EEBUSManager(settings2);

            manager1.Start();

            manager2.Start();
            manager1.OnDeviceDataChanged += deviceData =>
            {
                Console.WriteLine($"Manager1 new data: {deviceData}");
                return Task.CompletedTask;
            };
            manager2.OnDeviceDataChanged += deviceData =>
            {
                Console.WriteLine($"Manager2 new data: {deviceData}");
                return Task.CompletedTask;
            };
            manager1.OnDeviceFound += (sender, device) =>
            {
                Console.WriteLine($"Manager1 found device: {device.Name}");
            };

            manager2.OnDeviceFound += (sender, device) =>
            {
                Console.WriteLine($"Manager2 found device: {device.Name}");
            };

            manager1.OnDeviceConnectionStatusChanged += (remoteDevice, status) =>
            {
                Console.WriteLine($"Manager1 connection status changed: {remoteDevice.Name} is now {status}");
                return Task.CompletedTask;
            };

            manager2.OnDeviceConnectionStatusChanged += (remoteDevice, status) =>
            {
                Console.WriteLine($"Manager2 connection status changed: {remoteDevice.Name} is now {status}");
                return Task.CompletedTask;
            };

            manager1.AddTrustedSki(manager2.GetLocalData().SKI);
            manager2.AddTrustedSki(manager1.GetLocalData().SKI);

            await manager1.ConnectAsync(manager2.GetLocalData().SKI);
            await manager1.WriteDataAsync(new EEBUS.Net.EEBUS.Models.Data.DeviceData()
            {
                Lpc = new EEBUS.Net.EEBUS.Models.Data.LpcLppData() { Limit = 1000 }
            });
           
            await Task.Delay(50000);

        }
    }
}
