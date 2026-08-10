using EEBUS;
using EEBUS.Messages;
using EEBUS.Net;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace TestProject1.Bugs
{
    /*
     * Original log excerpt:
     * 10:35:44.297 <--- {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[2]},{"feat
       ure":1}]},{"addressDestination":[{"device":"EEBUS_162410145_21821"},{"entity":[0]},{"feature":0}]},{"msgCounter":13474},{"cmdClassifier":"call"},{"ackRequest":true}]},{"payload":[{"cmd":[[{"nodeManagementSubscript
       ionRequestCall":[{"subscriptionRequest":[{"clientAddress":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[2]},{"feature":1}]},{"serverAddress":[{"device":"EEBUS_162410145_21821"},{"entity":[2]},{"feature":2
       }]},{"serverFeatureType":"ElectricalConnection"}]}]}]]}]}]}}]}                                                                                                                                                       
                                                                                                                                                                                                                    
       Was waiting for Data: {"data":{"header":{"protocolId":"ee1.0"},"payload":{"datagram":{"header":{"specificationVersion":"1.3.0","addressSource":{"device":"i:51593_u:GTHE0300000086-CLS2","entity":[2],"feature":1},"a
       ddressDestination":{"device":"EEBUS_162410145_21821","entity":[0],"feature":0},"msgCounter":13474,"cmdClassifier":"call","ackRequest":true},"payload":{"cmd":[{"nodeManagementSubscriptionRequestCall":{"subscription
       Request":{"clientAddress":{"device":"i:51593_u:GTHE0300000086-CLS2","entity":[2],"feature":1},"serverAddress":{"device":"EEBUS_162410145_21821","entity":[2],"feature":2},"serverFeatureType":"ElectricalConnection"}
       }}]}}}}}                             
 
     */
    public class SubscriptionAndBindingMessagesNotProcessed : EebusTests
    {
        const string SubscriptionRequestCall = """{"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[2]},{"feature":1}]},{"addressDestination":[{"device":"EEBUS_162410145_21821"},{"entity":[0]},{"feature":0}]},{"msgCounter":13453},{"cmdClassifier":"call"},{"ackRequest":true}]},{"payload":[{"cmd":[[{"nodeManagementSubscriptionRequestCall":[{"subscriptionRequest":[{"clientAddress":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[2]},{"feature":1}]},{"serverAddress":[{"device":"EEBUS_162410145_21821"},{"entity":[2]},{"feature":1}]},{"serverFeatureType":"Measurement"}]}]}]]}]}]}}]}""";
        const string NodeDiscoveryData = """{"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[0]},{"feature":0}]},{"addressDestination":[{"device":"EEBUS_162410145_21821"},{"entity":[0]},{"feature":0}]},{"msgCounter":120},{"msgCounterReference":53},{"cmdClassifier":"reply"}]},{"payload":[{"cmd":[[{"nodeManagementDetailedDiscoveryData":[{"specificationVersionList":[{"specificationVersion":["1.3.0"]}]},{"deviceInformation":[{"description":[{"deviceAddress":[{"device":"i:51593_u:GTHE0300000086-CLS2"}]},{"deviceType":"Generic"},{"networkFeatureSet":"smart"},{"lastStateChange":"added"}]}]},{"entityInformation":[[{"description":[{"entityAddress":[{"entity":[0]}]},{"entityType":"DeviceInformation"}]}],[{"description":[{"entityAddress":[{"entity":[1]}]},{"entityType":"CEM"}]}],[{"description":[{"entityAddress":[{"entity":[2]}]},{"entityType":"CEM"}]}],[{"description":[{"entityAddress":[{"entity":[3]}]},{"entityType":"CEM"}]}],[{"description":[{"entityAddress":[{"entity":[4]}]},{"entityType":"CEM"}]}]]},{"featureInformation":[[{"description":[{"featureAddress":[{"entity":[0]},{"feature":0}]},{"featureType":"NodeManagement"},{"role":"special"},{"supportedFunction":[[{"function":"nodeManagementBindingData"},{"possibleOperations":[{"read":[]}]}],[{"function":"nodeManagementBindingDeleteCall"},{"possibleOperations":[]}],[{"function":"nodeManagementBindingRequestCall"},{"possibleOperations":[]}],[{"function":"nodeManagementDetailedDiscoveryData"},{"possibleOperations":[{"read":[]}]}],[{"function":"nodeManagementSubscriptionData"},{"possibleOperations":[{"read":[]}]}],[{"function":"nodeManagementSubscriptionDeleteCall"},{"possibleOperations":[]}],[{"function":"nodeManagementSubscriptionRequestCall"},{"possibleOperations":[]}],[{"function":"nodeManagementUseCaseData"},{"possibleOperations":[{"read":[]}]}]]}]}],[{"description":[{"featureAddress":[{"entity":[0]},{"feature":1}]},{"featureType":"DeviceClassification"},{"role":"server"},{"supportedFunction":[[{"function":"deviceClassificationManufacturerData"},{"possibleOperations":[{"read":[]}]}]]}]}],[{"description":[{"featureAddress":[{"entity":[1]},{"feature":1}]},{"featureType":"Generic"},{"role":"client"}]}],[{"description":[{"featureAddress":[{"entity":[1]},{"feature":1000}]},{"featureType":"DeviceDiagnosis"},{"role":"server"},{"supportedFunction":[[{"function":"deviceDiagnosisHeartbeatData"},{"possibleOperations":[{"read":[]}]}],[{"function":"deviceDiagnosisStateData"},{"possibleOperations":[{"read":[]}]}]]},{"description":"Device diagnosis server feature"}]}],[{"description":[{"featureAddress":[{"entity":[2]},{"feature":1}]},{"featureType":"Generic"},{"role":"client"}]}],[{"description":[{"featureAddress":[{"entity":[2]},{"feature":1000}]},{"featureType":"DeviceDiagnosis"},{"role":"server"},{"supportedFunction":[[{"function":"deviceDiagnosisHeartbeatData"},{"possibleOperations":[{"read":[]}]}],[{"function":"deviceDiagnosisStateData"},{"possibleOperations":[{"read":[]}]}]]},{"description":"Device diagnosis server feature"}]}],[{"description":[{"featureAddress":[{"entity":[3]},{"feature":1}]},{"featureType":"Generic"},{"role":"client"}]}],[{"description":[{"featureAddress":[{"entity":[3]},{"feature":1000}]},{"featureType":"DeviceDiagnosis"},{"role":"server"},{"supportedFunction":[[{"function":"deviceDiagnosisHeartbeatData"},{"possibleOperations":[{"read":[]}]}],[{"function":"deviceDiagnosisStateData"},{"possibleOperations":[{"read":[]}]}]]},{"description":"Device diagnosis server feature"}]}],[{"description":[{"featureAddress":[{"entity":[4]},{"feature":1}]},{"featureType":"Generic"},{"role":"client"}]}],[{"description":[{"featureAddress":[{"entity":[4]},{"feature":1000}]},{"featureType":"DeviceDiagnosis"},{"role":"server"},{"supportedFunction":[[{"function":"deviceDiagnosisHeartbeatData"},{"possibleOperations":[{"read":[]}]}],[{"function":"deviceDiagnosisStateData"},{"possibleOperations":[{"read":[]}]}]]},{"description":"Device diagnosis server feature"}]}]]}]}]]}]}]}}]}""";
        const string UseCaseDiscoveryData = """{"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[0]},{"feature":0}]},{"addressDestination":[{"device":"EEBUS_162410145_21821"},{"entity":[0]},{"feature":0}]},{"msgCounter":124},{"msgCounterReference":55},{"cmdClassifier":"reply"}]},{"payload":[{"cmd":[[{"nodeManagementUseCaseData":[{"useCaseInformation":[[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[1]}]},{"actor":"EnergyGuard"},{"useCaseSupport":[[{"useCaseName":"limitationOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}],[{"useCaseName":"limitationOfPowerProduction"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[1]}]},{"actor":"MonitoringAppliance"},{"useCaseSupport":[[{"useCaseName":"monitoringOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[2]}]},{"actor":"EnergyGuard"},{"useCaseSupport":[[{"useCaseName":"limitationOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}],[{"useCaseName":"limitationOfPowerProduction"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[2]}]},{"actor":"MonitoringAppliance"},{"useCaseSupport":[[{"useCaseName":"monitoringOfGridConnectionPoint"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5,6,7]},{"useCaseDocumentSubRevision":"release"}],[{"useCaseName":"monitoringOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[3]}]},{"actor":"EnergyGuard"},{"useCaseSupport":[[{"useCaseName":"limitationOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[3]}]},{"actor":"MonitoringAppliance"},{"useCaseSupport":[[{"useCaseName":"monitoringOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[4]}]},{"actor":"EnergyGuard"},{"useCaseSupport":[[{"useCaseName":"limitationOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}],[{"useCaseName":"limitationOfPowerProduction"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[4]}]},{"actor":"MonitoringAppliance"},{"useCaseSupport":[[{"useCaseName":"monitoringOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5]},{"useCaseDocumentSubRevision":"release"}]]}]]}]}]]}]}]}}]}""";


        [Fact]
        public async Task WasWaitingForData_On_BindingOrSubscriptionRequest()
        {
            Connection connection = GetDefaultMockConnection();

            SetDiscoveryData(connection, NodeDiscoveryData);
            SetUseCaseData(connection, UseCaseDiscoveryData);

            //SpineDatagramPayload discoveryPayload = GetPayload(SubscriptionRequestCall);
            //if (discoveryPayload.datagram == null) throw new Exception("No datagram for message found");

            //NodeManagementSubscriptionRequestCall? discoveryData = JsonHelper.FromJsonNode<NodeManagementSubscriptionRequestCall>(discoveryPayload.datagram.payload);
            //if (discoveryData == null) throw new Exception("Failed to parse discovery data");

            //var bytes = Encoding.UTF8.GetBytes(SubscriptionRequestCall);
            //var span = bytes.AsSpan();
            //ShipMessageBase? message = ShipMessageBase.Create(span) ?? throw new Exception("Failed to create ShipMessage");
            SpineDatagramPayload payload = GetPayload(SubscriptionRequestCall);
            SpineDatagramPayload? answer = await payload.CreateAnswerAsync(DataMessage.NextCount, connection);
            Assert.NotNull(answer);
        }

        protected override DeviceSettings GetDeviceSettings()
        {
            //return new DeviceSettings()
            //{
            //    Name = "ConsoleDemoDevice",
            //    Id = "Kermi-EEBUS-Demo-Client",
            //    Model = "KermiDemo",
            //    Brand = "Kermi",
            //    Type = "EnergyManagementSystem",
            //    Serial = "123456",
            //    Port = 7200,
            //    Entities = [
            //            new EntitySettings { Type = "DeviceInformation" },
            //            new EntitySettings { Type  = "CEM", UseCases = [
            //                new UseCaseSettings {
            //                    Type = "limitationOfPowerConsumption",
            //                    Actor = "ControllableSystem",
            //                    InitLimits = new LimitSettings {
            //                        Active = false,
            //                        Limit = 4300,
            //                        Duration = TimeSpan.FromSeconds(7200),
            //                        FailsafeLimit = 7200,
            //                        NominalMax = 40000
            //                    }
            //                },
            //                new UseCaseSettings {
            //                    Type = "limitationOfPowerProduction",
            //                    Actor = "ControllableSystem",
            //                    InitLimits = new LimitSettings {
            //                        Active = false,
            //                        Limit = 4300,
            //                        Duration = TimeSpan.FromSeconds(7200),
            //                        FailsafeLimit = 7200,
            //                        NominalMax = 40000
            //                    }
            //                }
            //                ]}
            //            ]

            //};

            return new DeviceSettings()
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
                                    Duration = TimeSpan.FromSeconds(7200),
                                    FailsafeLimit = 7200,
                                    NominalMax = 40000
                                }
                            },
                            new UseCaseSettings {
                                Type = "monitoringOfGridConnectionPoint",
                                Actor = "MonitoringAppliance"
                            },
                            new UseCaseSettings {
                                Type = "monitoringOfPowerConsumption",
                                Actor = "MonitoringAppliance"
                            }
                        ]},
                        new EntitySettings { Type = "SubMeterElectricity", UseCases = [
                            new UseCaseSettings {
                                Type = "monitoringOfPowerConsumption",
                                Actor = "MonitoredUnit"
                            }
                        ]}
                    ]
            };
        }
    }
}
