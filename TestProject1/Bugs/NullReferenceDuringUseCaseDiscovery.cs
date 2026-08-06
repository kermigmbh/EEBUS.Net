using EEBUS;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace TestProject1.Bugs
{
    /*
     * Original exception log:
     * 
     * 14:00:33.715 <--- {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[0]},{"feature":0}]},{"addressDestination":[{"device":"EEBUS_162410145_21821"},{"entity":[0]},{"feature":0}]},{"msgCounter":124},{"msgCounterReference":55},{"cmdClassifier":"reply"}]},{"payload":[{"cmd":[[{"nodeManagementUseCaseData":[{"useCaseInformation":[[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[1]}]},{"actor":"EnergyGuard"},{"useCaseSupport":[[{"useCaseName":"limitationOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}],[{"useCaseName":"limitationOfPowerProduction"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[1]}]},{"actor":"MonitoringAppliance"},{"useCaseSupport":[[{"useCaseName":"monitoringOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[2]}]},{"actor":"EnergyGuard"},{"useCaseSupport":[[{"useCaseName":"limitationOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}],[{"useCaseName":"limitationOfPowerProduction"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[2]}]},{"actor":"MonitoringAppliance"},{"useCaseSupport":[[{"useCaseName":"monitoringOfGridConnectionPoint"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5,6,7]},{"useCaseDocumentSubRevision":"release"}],[{"useCaseName":"monitoringOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[3]}]},{"actor":"EnergyGuard"},{"useCaseSupport":[[{"useCaseName":"limitationOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[3]}]},{"actor":"MonitoringAppliance"},{"useCaseSupport":[[{"useCaseName":"monitoringOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[4]}]},{"actor":"EnergyGuard"},{"useCaseSupport":[[{"useCaseName":"limitationOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}],[{"useCaseName":"limitationOfPowerProduction"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[4]}]},{"actor":"MonitoringAppliance"},{"useCaseSupport":[[{"useCaseName":"monitoringOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5]},{"useCaseDocumentSubRevision":"release"}]]}]]}]}]]}]}]}}]}

        Client connection closed with error. | Exception: System.NullReferenceException: Object reference not set to an instance of an object.
           at EEBUS.Models.Entity.<>c__DisplayClass28_0.<GetOrAdd>b__0(Feature f)
           at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Func`2 predicate, Boolean& found)
           at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source, Func`2 predicate)
           at EEBUS.Models.Entity.GetOrAdd(Feature feature)
           at EEBUS.UseCases.EnergyGuard.LimitationOfPowerConsumption..ctor(UseCaseSettings usecaseSettings, Entity entity)
           at EEBUS.UseCases.EnergyGuard.LimitationOfPowerConsumption.Class.Create(UseCaseSettings usecaseSettings, Entity entity)
           at EEBUS.Models.UseCase.Create(UseCaseSettings usecaseSettings, Entity entity)
           at EEBUS.Models.Device.SetUseCaseData(NodeManagementUseCaseData useCaseData)
           at EEBUS.SPINE.Commands.NodeManagementUseCaseData.Class.EvaluateAsync(Connection connection, DatagramType datagram)
           at EEBUS.Messages.SpineDatagramPayload.EvaluateAsync(Connection connection)
           at EEBUS.SHIP.Messages.DataMessage.NextServerState(Connection connection, ILogger logger)
           at EEBUS.Messages.ShipMessageBase.NextClientState(Connection connection, ILogger logger)
           at EEBUS.Client.RunInternalAsync(CancellationToken cancellationToken)
     */

    public class NullReferenceDuringUseCaseDiscovery : EebusTests
    {

        const string UseCaseDiscoveryData = """{"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[0]},{"feature":0}]},{"addressDestination":[{"device":"EEBUS_162410145_21821"},{"entity":[0]},{"feature":0}]},{"msgCounter":124},{"msgCounterReference":55},{"cmdClassifier":"reply"}]},{"payload":[{"cmd":[[{"nodeManagementUseCaseData":[{"useCaseInformation":[[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[1]}]},{"actor":"EnergyGuard"},{"useCaseSupport":[[{"useCaseName":"limitationOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}],[{"useCaseName":"limitationOfPowerProduction"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[1]}]},{"actor":"MonitoringAppliance"},{"useCaseSupport":[[{"useCaseName":"monitoringOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[2]}]},{"actor":"EnergyGuard"},{"useCaseSupport":[[{"useCaseName":"limitationOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}],[{"useCaseName":"limitationOfPowerProduction"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[2]}]},{"actor":"MonitoringAppliance"},{"useCaseSupport":[[{"useCaseName":"monitoringOfGridConnectionPoint"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5,6,7]},{"useCaseDocumentSubRevision":"release"}],[{"useCaseName":"monitoringOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[3]}]},{"actor":"EnergyGuard"},{"useCaseSupport":[[{"useCaseName":"limitationOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[3]}]},{"actor":"MonitoringAppliance"},{"useCaseSupport":[[{"useCaseName":"monitoringOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[4]}]},{"actor":"EnergyGuard"},{"useCaseSupport":[[{"useCaseName":"limitationOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}],[{"useCaseName":"limitationOfPowerProduction"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4]},{"useCaseDocumentSubRevision":"release"}]]}],[{"address":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[4]}]},{"actor":"MonitoringAppliance"},{"useCaseSupport":[[{"useCaseName":"monitoringOfPowerConsumption"},{"useCaseVersion":"1.0.0"},{"useCaseAvailable":true},{"scenarioSupport":[1,2,3,4,5]},{"useCaseDocumentSubRevision":"release"}]]}]]}]}]]}]}]}}]}""";
        const string NodeDiscoveryData = """{"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[0]},{"feature":0}]},{"addressDestination":[{"device":"EEBUS_162410145_21821"},{"entity":[0]},{"feature":0}]},{"msgCounter":120},{"msgCounterReference":53},{"cmdClassifier":"reply"}]},{"payload":[{"cmd":[[{"nodeManagementDetailedDiscoveryData":[{"specificationVersionList":[{"specificationVersion":["1.3.0"]}]},{"deviceInformation":[{"description":[{"deviceAddress":[{"device":"i:51593_u:GTHE0300000086-CLS2"}]},{"deviceType":"Generic"},{"networkFeatureSet":"smart"},{"lastStateChange":"added"}]}]},{"entityInformation":[[{"description":[{"entityAddress":[{"entity":[0]}]},{"entityType":"DeviceInformation"}]}],[{"description":[{"entityAddress":[{"entity":[1]}]},{"entityType":"CEM"}]}],[{"description":[{"entityAddress":[{"entity":[2]}]},{"entityType":"CEM"}]}],[{"description":[{"entityAddress":[{"entity":[3]}]},{"entityType":"CEM"}]}],[{"description":[{"entityAddress":[{"entity":[4]}]},{"entityType":"CEM"}]}]]},{"featureInformation":[[{"description":[{"featureAddress":[{"entity":[0]},{"feature":0}]},{"featureType":"NodeManagement"},{"role":"special"},{"supportedFunction":[[{"function":"nodeManagementBindingData"},{"possibleOperations":[{"read":[]}]}],[{"function":"nodeManagementBindingDeleteCall"},{"possibleOperations":[]}],[{"function":"nodeManagementBindingRequestCall"},{"possibleOperations":[]}],[{"function":"nodeManagementDetailedDiscoveryData"},{"possibleOperations":[{"read":[]}]}],[{"function":"nodeManagementSubscriptionData"},{"possibleOperations":[{"read":[]}]}],[{"function":"nodeManagementSubscriptionDeleteCall"},{"possibleOperations":[]}],[{"function":"nodeManagementSubscriptionRequestCall"},{"possibleOperations":[]}],[{"function":"nodeManagementUseCaseData"},{"possibleOperations":[{"read":[]}]}]]}]}],[{"description":[{"featureAddress":[{"entity":[0]},{"feature":1}]},{"featureType":"DeviceClassification"},{"role":"server"},{"supportedFunction":[[{"function":"deviceClassificationManufacturerData"},{"possibleOperations":[{"read":[]}]}]]}]}],[{"description":[{"featureAddress":[{"entity":[1]},{"feature":1}]},{"featureType":"Generic"},{"role":"client"}]}],[{"description":[{"featureAddress":[{"entity":[1]},{"feature":1000}]},{"featureType":"DeviceDiagnosis"},{"role":"server"},{"supportedFunction":[[{"function":"deviceDiagnosisHeartbeatData"},{"possibleOperations":[{"read":[]}]}],[{"function":"deviceDiagnosisStateData"},{"possibleOperations":[{"read":[]}]}]]},{"description":"Device diagnosis server feature"}]}],[{"description":[{"featureAddress":[{"entity":[2]},{"feature":1}]},{"featureType":"Generic"},{"role":"client"}]}],[{"description":[{"featureAddress":[{"entity":[2]},{"feature":1000}]},{"featureType":"DeviceDiagnosis"},{"role":"server"},{"supportedFunction":[[{"function":"deviceDiagnosisHeartbeatData"},{"possibleOperations":[{"read":[]}]}],[{"function":"deviceDiagnosisStateData"},{"possibleOperations":[{"read":[]}]}]]},{"description":"Device diagnosis server feature"}]}],[{"description":[{"featureAddress":[{"entity":[3]},{"feature":1}]},{"featureType":"Generic"},{"role":"client"}]}],[{"description":[{"featureAddress":[{"entity":[3]},{"feature":1000}]},{"featureType":"DeviceDiagnosis"},{"role":"server"},{"supportedFunction":[[{"function":"deviceDiagnosisHeartbeatData"},{"possibleOperations":[{"read":[]}]}],[{"function":"deviceDiagnosisStateData"},{"possibleOperations":[{"read":[]}]}]]},{"description":"Device diagnosis server feature"}]}],[{"description":[{"featureAddress":[{"entity":[4]},{"feature":1}]},{"featureType":"Generic"},{"role":"client"}]}],[{"description":[{"featureAddress":[{"entity":[4]},{"feature":1000}]},{"featureType":"DeviceDiagnosis"},{"role":"server"},{"supportedFunction":[[{"function":"deviceDiagnosisHeartbeatData"},{"possibleOperations":[{"read":[]}]}],[{"function":"deviceDiagnosisStateData"},{"possibleOperations":[{"read":[]}]}]]},{"description":"Device diagnosis server feature"}]}]]}]}]]}]}]}}]}""";

        [Fact]
        public async Task NullReferenceDuringUseCaseDiscoveryAsync()
        {
            Connection connection = GetMockConnection();

            SetDiscoveryData(connection);
            SetUseCaseData(connection);
        }

        private string JsonFromEEBUSJson(string json)
        {

            json = json.Replace("[{", "{");
            json = json.Replace("},{", ",");
            json = json.Replace("}]", "}");
            json = json.Replace("[]", "{}");
            return json;
        }

        private Connection GetMockConnection()
        {
            var devices = new Devices();
            devices.GetOrCreateLocal(GetSkiBytes(DefaultLocalSki), GetDeviceSettings());
            var remoteDevice = devices.GetOrCreateRemote("TestRemote", DefaultRemoteSki, string.Empty, "TestRemote");
            var client = new Client(default, default, devices, remoteDevice);
            return client;
        }

        private void SetDiscoveryData(Connection connection)
        {
            if (connection.Remote == null) return;

            SpineDatagramPayload discoveryPayload = GetPayload(NodeDiscoveryData);
            if (discoveryPayload.datagram == null) throw new Exception("No datagram for message found");

            NodeManagementDetailedDiscoveryData? discoveryData = JsonSerializer.Deserialize<NodeManagementDetailedDiscoveryData>(discoveryPayload.datagram.payload);
            if (discoveryData == null) throw new Exception("Failed to parse discovery data");

            connection.Remote.SetDiscoveryData(discoveryData, connection);
        }

        private void SetUseCaseData(Connection connection)
        {
            if (connection.Remote == null) return;

            SpineDatagramPayload discoveryPayload = GetPayload(UseCaseDiscoveryData);
            if (discoveryPayload.datagram == null) throw new Exception("No datagram for message found");

            NodeManagementUseCaseData? useCaseData = JsonSerializer.Deserialize<NodeManagementUseCaseData>(discoveryPayload.datagram.payload);
            if (useCaseData == null) throw new Exception("Failed to parse discovery data");

            connection.Remote.SetUseCaseData(useCaseData);
        }

        private SpineDatagramPayload GetPayload(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var parsedMessage = ShipMessageBase.Create(bytes) as DataMessage;
            return parsedMessage?.data.payload.Deserialize<SpineDatagramPayload>() ?? throw new Exception("Failed to create payload");
        }

        private DeviceSettings GetDeviceSettings()
        {
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
                                Type = "limitationOfPowerProduction",
                                Actor = "ControllableSystem",
                                InitLimits = new LimitSettings {
                                    Active = false,
                                    Limit = 4300,
                                    Duration = TimeSpan.FromSeconds(7200),
                                    FailsafeLimit = 7200,
                                    NominalMax = 40000
                                }
                            }
                            ]}
                        ]

            };
        }

    }
}
