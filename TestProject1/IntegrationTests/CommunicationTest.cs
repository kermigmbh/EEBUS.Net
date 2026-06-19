using EEBUS;
using EEBUS.Models;
using EEBUS.Net;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit.Abstractions;

namespace TestProject1.IntegrationTests
{
    public class CommunicationTest
    {
        private readonly ITestOutputHelper _output;

        public CommunicationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private void Log(string message)
        {
            _output.WriteLine(message);
            Debug.WriteLine(message);
        }

        private sealed class TestOutputLogger(ITestOutputHelper output, string categoryName) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                var message = formatter(state, exception);
                Console.WriteLine($"[{categoryName}] {logLevel}: {message}");

                if (exception is not null)
                {
                    Console.WriteLine(exception.ToString());
                }
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();

                public void Dispose()
                {
                }
            }
        }

        [Fact]
        public async Task TwoEEBUSDevicesConnectivityAsync()
        {
            var settings1 = new Settings()
            {
                Device = new DeviceSettings()
                {
                    Name = "KermiConsumer",
                    Id = "KermiConsumer",
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
                    Name = "KermiControlbox",
                    Id = "KermiControlbox",
                    Model = "KermiDemo",
                    Brand = "Kermi",
                    Type = "EnergyGuard",
                    Serial = "444444",
                    Port = 7201,
                    Entities = [
                      new EntitySettings { Type = "DeviceInformation" },
                        new EntitySettings { Type  = "GridGuard", UseCases = [
                            new UseCaseSettings {
                                Type = "limitationOfPowerConsumption",
                                Actor = "EnergyGuard",
                                InitLimits = new LimitSettings {
                                    Active = false,
                                    Limit = 0,
                                    Duration = Timeout.InfiniteTimeSpan,
                                    FailsafeLimit = 7200,
                                    FailsafeDurationMinimum = TimeSpan.FromHours(2),
                                    NominalMax = 40000
                                }
                            }

                            ]}
                          ]
                },
                //Certificate = "XCenterEEBUS"
                Certificate = "EEBUS2.net"
            };
            ILogger manager1Logger = new TestOutputLogger(_output, "Manager1");
            ILogger manager2Logger = new TestOutputLogger(_output, "Manager2");

            EEBUSManager manager1 = new EEBUSManager(settings1, logger: manager1Logger);

            EEBUSManager manager2 = new EEBUSManager(settings2, logger: manager2Logger);

            manager1.Start();
            manager2.Start();


            manager1.OnDeviceDataChanged += deviceData =>
            {
                Log($"Manager1 new data: {deviceData.Lpc?.Limit}");
                return Task.CompletedTask;
            };
            manager2.OnDeviceDataChanged += deviceData =>
            {
                if (deviceData.Lpc != null)
                    Log($"Manager2 new data: {deviceData.Lpc?.Limit}");
                return Task.CompletedTask;
            };
            manager1.OnDeviceFound += (sender, device) =>
            {
                Log($"Manager1 found device: {device.Name}");
            };

            manager2.OnDeviceFound += (sender, device) =>
            {
                Log($"Manager2 found device: {device.Name}");
            };

            manager1.OnDeviceConnectionStatusChanged += (remoteDevice, status) =>
            {
                Log($"Manager1 connection status changed: {remoteDevice.Name} is now {status}");
                return Task.CompletedTask;
            };

            manager2.OnDeviceConnectionStatusChanged += (remoteDevice, status) =>
            {
                Log($"Manager2 connection status changed: {remoteDevice.Name} is now {status}");
                return Task.CompletedTask;
            };

            //await Task.Delay(5000);

            manager1.AddTrustedSki(manager2.GetLocalData().SKI);
            manager2.AddTrustedSki(manager1.GetLocalData().SKI);

            bool success = false;

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    success = await manager1.TryConnectAsync(manager2.GetLocalData().SKI);
                }
                catch (Exception ex)
                {
                    Log(ex.ToString());
                    await Task.Delay(2000);
                }

                await Task.Delay(1000);
                if (success == true)
                {
                    Log("Connection established successfully.");
                    break;
                }
                else
                {
                    Log("Connection attempt failed. Retrying...");
                    await Task.Delay(2000);
                }
            }

            if (success == false)
            {
                Log("Failed to establish connection after multiple attempts.");
                throw new Exception("Failed to establish connection after multiple attempts.");
            }

            for (int i = 0; i < 50; i++)
            {
                await Task.Delay(5000);
                Log("Sending data");
                await manager2.WriteDataAsync(new EEBUS.Net.EEBUS.Models.Data.DeviceData()
                {
                    Lpc = new EEBUS.Net.EEBUS.Models.Data.LpcLppData() { Limit = 1000, LimitActive = true }
                }, manager1.GetLocalData().SKI);

            }



            await Task.Delay(50000);

        }
    }
}
