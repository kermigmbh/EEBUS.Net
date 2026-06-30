using EEBUS;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace TestProject1.IntegrationTests
{
    public static  class Setup
    {
        //_nodeNumber ensures we do not create the same node multiple times, which could influence the test results
        private static int _nodeNumber = 0;
        private static object _lock = new object();

        public static Settings GetCEMSettings([CallerMemberName] string methodName = "")
        {
            lock (_lock)
            {
                _nodeNumber++;
                var settings1 = new Settings()
                {
                    Device = new DeviceSettings()
                    {
                        Name = "KermiConsumer",
                        Id = "KermiConsumer-" + _nodeNumber,
                        Model = "KermiDemo",
                        Brand = "Kermi",
                        Type = "EnergyManagementSystem",
                        Serial = "123456",
                        Port = (ushort)(7000 + _nodeNumber),
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
                    Certificate = "EEBUS" + (_nodeNumber) + ".net"
                };
                return settings1;
            }
        }


        public static Settings GetControlBoxSettings([CallerMemberName] string methodName = "")
        {
            lock (_lock)
            {
                _nodeNumber++;
                var settings2 = new Settings()
                {
                    Device = new DeviceSettings()
                    {
                        Name = "KermiControlbox",
                        Id = "KermiControlbox-" + _nodeNumber,
                        Model = "KermiDemo",
                        Brand = "Kermi",
                        Type = "EnergyGuard",
                        Serial = "444444",
                        Port = (ushort)(7200 + _nodeNumber),
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
                    Certificate = "EEBUS" + (_nodeNumber) + ".net"
                };
                return settings2;
            }
        }

    }
}
