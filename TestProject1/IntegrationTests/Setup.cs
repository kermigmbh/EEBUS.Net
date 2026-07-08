using EEBUS;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace TestProject1.IntegrationTests
{
    public static class Setup
    {
        //_nodeNumber ensures we do not create the same node multiple times, which could influence the test results
        private static int _nodeNumber = 0;
        private static object _lock = new object();

        public static Settings GetCEMSettings(LimitSettings? initLimits = null)
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
                                InitLimits = initLimits ?? new LimitSettings {
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


        public static Settings GetControlBoxSettings(LimitSettings? initLimits = null)
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
                                    InitLimits = initLimits ?? new LimitSettings {
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
                    Certificate = "EEBUS" + _nodeNumber + ".net"
                };
                return settings2;
            }
        }

        public static Settings GetEMeterSettings(MeasurementSettings? initMeasurements = null)
        {
            lock (_lock)
            {
                _nodeNumber++;
                var settings = new Settings()
                {
                    Device = new DeviceSettings()
                    {
                        Name = "EMeter",
                        Id = "EMeter-" + _nodeNumber,
                        Model = "Demo EMeter",
                        Brand = "Kermi",
                        Type = "EnergyMeter",
                        Serial = "555555",
                        Port = (ushort)(7300 + _nodeNumber),
                        Entities = [
                            new EntitySettings { Type = "DeviceInformation" },
                            new EntitySettings { Type = "SubMeterElectricity", UseCases = [
                                new UseCaseSettings {
                                    Type = "monitoringOfPowerConsumption",
                                    Actor = "MonitoredUnit",
                                    InitMeasurements = initMeasurements
                                }
                            ]}
                        ]
                    },
                    Certificate = "EEBUS" + _nodeNumber + ".net"
                };
                return settings;
            }
        }

        public static Settings GetGridConnectionPointSettings(MeasurementSettings? initMeasurements = null)
        {
            lock (_lock)
            {
                _nodeNumber++;
                var settings = new Settings()
                {
                    Device = new DeviceSettings()
                    {
                        Name = "Grid Connection Point",
                        Id = "GridConnectionPoint-" + _nodeNumber,
                        Model = "Demo Grid Connection Point",
                        Brand = "Kermi",
                        Type = "GCP",
                        Serial = "555555",
                        Port = (ushort)(7300 + _nodeNumber),
                        Entities = [
                            new EntitySettings { Type = "DeviceInformation" },
                            new EntitySettings { Type = "GridConnectionPointOfPremises", UseCases = [
                                new UseCaseSettings {
                                    Type = "monitoringOfGridConnectionPoint",
                                    Actor = "GridConnectionPoint",
                                    InitMeasurements = initMeasurements
                                }
                            ]}
                        ]
                    },
                    Certificate = "EEBUS" + _nodeNumber + ".net"
                };
                return settings;
            }
        }

        public static Settings GetEMeterMonitorSettings(MeasurementSettings? initMeasurements = null)
        {
            lock (_lock)
            {
                _nodeNumber++;
                var settings = new Settings()
                {
                    Device = new DeviceSettings()
                    {
                        Name = "EMeter Monitor",
                        Id = "EMeterMonitor-" + _nodeNumber,
                        Model = "Demo EMeter Monitor",
                        Brand = "Kermi",
                        Type = "EnergyManagementSystem",
                        Serial = "555555",
                        Port = (ushort)(7300 + _nodeNumber),
                        Entities = [
                            new EntitySettings { Type = "DeviceInformation" },
                            new EntitySettings { Type = "CEM", UseCases = [
                                new UseCaseSettings {
                                    Type = "monitoringOfPowerConsumption",
                                    Actor = "MonitoringAppliance",
                                    InitMeasurements = initMeasurements
                                },
                                new UseCaseSettings {
                                    Type = "monitoringOfGridConnectionPoint",
                                    Actor = "MonitoringAppliance",
                                    InitMeasurements = initMeasurements
                                }
                            ]}
                        ]
                    },
                    
                    Certificate = "EEBUS" + _nodeNumber + ".net"
                };
                return settings;
            }
        }
    }
}
