using EEBUS;
using EEBUS.Messages;
using EEBUS.Net;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
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

        private static SpineDatagramPayload GetPayload(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var parsedMessage = ShipMessageBase.Create(bytes) as DataMessage;
            if (parsedMessage == null) throw new Exception("Failed to parse message");

            SpineDatagramPayload? payload = JsonHelper.FromJsonNode<SpineDatagramPayload>(parsedMessage.data.payload);
            if (payload == null) throw new Exception("Failed to create payload");

            return payload;
        }

        public static Settings GetSettingsFromDiscoveryData(string nodeManagementDetailedDiscoveryMessage, string nodeManagementUseCaseDataMessaage)
        {
            SpineDatagramPayload discoveryPayload = GetPayload(nodeManagementDetailedDiscoveryMessage);
            NodeManagementDetailedDiscoveryData? discoveryMessage = JsonHelper.FromJsonNode<NodeManagementDetailedDiscoveryData>(discoveryPayload.datagram.payload);
            if (discoveryMessage == null) throw new Exception("Failed to parse discovery message");

            SpineDatagramPayload useCaseDataPayload = GetPayload(nodeManagementUseCaseDataMessaage);
            NodeManagementUseCaseData? useCaseDataMessage = JsonHelper.FromJsonNode<NodeManagementUseCaseData>(useCaseDataPayload.datagram.payload);
            if (useCaseDataMessage == null) throw new Exception("Failed to parse use case data message");

            lock (_lock)
            {
                _nodeNumber++;
                var settings = new Settings()
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
                    },
                    Certificate = "EEBUS" + (_nodeNumber) + ".net"
                };

                EntityInformationType[]? entitySettings = discoveryMessage.cmd.First().nodeManagementDetailedDiscoveryData.entityInformation;
                if (entitySettings == null) throw new Exception("Failed to parse entity settings");

                List<EntitySettings> entities = [];
                foreach (EntityInformationType entity in entitySettings)
                {
                    List<UseCaseSettings> useCases = [];
                    IEnumerable<UseCaseInformationType>? entityUseCases = useCaseDataMessage.cmd.First().nodeManagementUseCaseData.useCaseInformation?.Where(uci => uci.address.entity.SequenceEqual(entity.description.entityAddress.entity));

                    foreach (UseCaseInformationType entityUseCase in entityUseCases ?? [])
                    {
                        foreach (UseCaseSupportType useCaseSupport in entityUseCase.useCaseSupport)
                        {
                            if (!useCaseSupport.useCaseAvailable) continue;

                            useCases.Add(new UseCaseSettings
                            {
                                Actor = entityUseCase.actor,
                                Type = useCaseSupport.useCaseName,
                                SupportedScenarios = useCaseSupport.scenarioSupport
                            });
                        }
                    }

                    entities.Add(new EntitySettings { Type = entity.description.entityType, UseCases = useCases.ToArray() });
                }

                settings.Device.Entities = entities.ToArray();
                return settings;
            }
        }

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

        public static Settings GetGridConnectionPointSettings(MeasurementSettings? initMeasurements = null, int? pvCurtailmentLimitFactor = null)
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
                                    InitMeasurements = initMeasurements,
                                    PvCurtailmentLimitFactor = pvCurtailmentLimitFactor ?? 0
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

        public static Settings GetControlBoxWithMultipleCEMEntitiesSettings(LimitSettings? initLimits = null)
        {
            lock (_lock)
            {
                _nodeNumber++;
                var settings = new Settings()
                {
                    Device = new DeviceSettings()
                    {
                        Name = "Controlbox CLS2",
                        Id = "ControlboxCLS2-" + _nodeNumber,
                        Model = "Demo Controlbox CLS2",
                        Brand = "Kermi",
                        Type = "EnergyManagementSystem",
                        Serial = "555555",
                        Port = (ushort)(7300 + _nodeNumber),
                        Entities = [
                            new EntitySettings { Type = "DeviceInformation" },
                            new EntitySettings { Type = "CEM", UseCases = [
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
                                },
                                new UseCaseSettings {
                                    Type = "limitationOfPowerProduction",
                                    Actor = "EnergyGuard",
                                    InitLimits = initLimits ?? new LimitSettings {
                                        Active = false,
                                        Limit = 0,
                                        Duration = Timeout.InfiniteTimeSpan,
                                        FailsafeLimit = 7200,
                                        FailsafeDurationMinimum = TimeSpan.FromHours(2),
                                        NominalMax = 40000
                                    }
                                },
                            ]},
                            new EntitySettings { Type = "CEM", UseCases = [
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
                                },
                                new UseCaseSettings {
                                    Type = "limitationOfPowerProduction",
                                    InitLimits = initLimits ?? new LimitSettings {
                                        Active = false,
                                        Limit = 0,
                                        Duration = Timeout.InfiniteTimeSpan,
                                        FailsafeLimit = 7200,
                                        FailsafeDurationMinimum = TimeSpan.FromHours(2),
                                        NominalMax = 40000
                                    }
                                },
                            ]},
                            new EntitySettings { Type = "CEM", UseCases = [
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
                                },
                                new UseCaseSettings {
                                    Type = "limitationOfPowerProduction",
                                    Actor = "EnergyGuard",
                                    InitLimits = initLimits ?? new LimitSettings {
                                        Active = false,
                                        Limit = 0,
                                        Duration = Timeout.InfiniteTimeSpan,
                                        FailsafeLimit = 7200,
                                        FailsafeDurationMinimum = TimeSpan.FromHours(2),
                                        NominalMax = 40000
                                    }
                                },
                            ]},
                            new EntitySettings { Type = "CEM", UseCases = [
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
                                },
                                new UseCaseSettings {
                                    Type = "limitationOfPowerProduction",
                                    Actor = "EnergyGuard",
                                    InitLimits = initLimits ?? new LimitSettings {
                                        Active = false,
                                        Limit = 0,
                                        Duration = Timeout.InfiniteTimeSpan,
                                        FailsafeLimit = 7200,
                                        FailsafeDurationMinimum = TimeSpan.FromHours(2),
                                        NominalMax = 40000
                                    }
                                },
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
