using EEBUS;
using System;
using System.Collections.Generic;
using System.Text;

namespace TestProject1.IntegrationTests
{
    public static  class Setup
    {

        public static Settings GetCEMSettings()
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
            return settings1;
        }


        public static Settings GetControlBoxSettings()
        {
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
            return settings2;
        }

    }
}
