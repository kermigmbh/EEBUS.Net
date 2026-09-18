using EEBUS;
using EEBUS.Messages;
using EEBUS.SHIP.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using TestProject1.IntegrationTests;

namespace TestProject1.ConsistencyTests
{
    public class HeartbeatSubscriptionWithMultipleEnergyGuards : EebusTests
    {
        string MultipleEnergyGuardssDetailedDiscoveryData = """{"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[0]},{"feature":0}]},{"addressDestination":[{"device":"EEBUS_162410145_21821"},{"entity":[0]},{"feature":0}]},{"msgCounter":66222},{"msgCounterReference":1},{"cmdClassifier":"reply"}]},{"payload":[{"cmd":[[{"nodeManagementDetailedDiscoveryData":[{"specificationVersionList":[{"specificationVersion":["1.3.0"]}]},{"deviceInformation":[{"description":[{"deviceAddress":[{"device":"i:51593_u:GTHE0300000086-CLS2"}]},{"deviceType":"Generic"},{"networkFeatureSet":"smart"},{"lastStateChange":"added"}]}]},{"entityInformation":[[{"description":[{"entityAddress":[{"entity":[0]}]},{"entityType":"DeviceInformation"}]}],[{"description":[{"entityAddress":[{"entity":[1]}]},{"entityType":"CEM"}]}],[{"description":[{"entityAddress":[{"entity":[2]}]},{"entityType":"CEM"}]}],[{"description":[{"entityAddress":[{"entity":[3]}]},{"entityType":"CEM"}]}],[{"description":[{"entityAddress":[{"entity":[4]}]},{"entityType":"CEM"}]}]]},{"featureInformation":[[{"description":[{"featureAddress":[{"entity":[0]},{"feature":0}]},{"featureType":"NodeManagement"},{"role":"special"},{"supportedFunction":[[{"function":"nodeManagementBindingData"},{"possibleOperations":[{"read":[]}]}],[{"function":"nodeManagementBindingDeleteCall"},{"possibleOperations":[]}],[{"function":"nodeManagementBindingRequestCall"},{"possibleOperations":[]}],[{"function":"nodeManagementDetailedDiscoveryData"},{"possibleOperations":[{"read":[]}]}],[{"function":"nodeManagementSubscriptionData"},{"possibleOperations":[{"read":[]}]}],[{"function":"nodeManagementSubscriptionDeleteCall"},{"possibleOperations":[]}],[{"function":"nodeManagementSubscriptionRequestCall"},{"possibleOperations":[]}],[{"function":"nodeManagementUseCaseData"},{"possibleOperations":[{"read":[]}]}]]}]}],[{"description":[{"featureAddress":[{"entity":[0]},{"feature":1}]},{"featureType":"DeviceClassification"},{"role":"server"},{"supportedFunction":[[{"function":"deviceClassificationManufacturerData"},{"possibleOperations":[{"read":[]}]}]]}]}],[{"description":[{"featureAddress":[{"entity":[1]},{"feature":1}]},{"featureType":"Generic"},{"role":"client"}]}],[{"description":[{"featureAddress":[{"entity":[1]},{"feature":1000}]},{"featureType":"DeviceDiagnosis"},{"role":"server"},{"supportedFunction":[[{"function":"deviceDiagnosisHeartbeatData"},{"possibleOperations":[{"read":[]}]}],[{"function":"deviceDiagnosisStateData"},{"possibleOperations":[{"read":[]}]}]]},{"description":"Device diagnosis server feature"}]}],[{"description":[{"featureAddress":[{"entity":[2]},{"feature":1}]},{"featureType":"Generic"},{"role":"client"}]}],[{"description":[{"featureAddress":[{"entity":[2]},{"feature":1000}]},{"featureType":"DeviceDiagnosis"},{"role":"server"},{"supportedFunction":[[{"function":"deviceDiagnosisHeartbeatData"},{"possibleOperations":[{"read":[]}]}],[{"function":"deviceDiagnosisStateData"},{"possibleOperations":[{"read":[]}]}]]},{"description":"Device diagnosis server feature"}]}],[{"description":[{"featureAddress":[{"entity":[3]},{"feature":1}]},{"featureType":"Generic"},{"role":"client"}]}],[{"description":[{"featureAddress":[{"entity":[3]},{"feature":1000}]},{"featureType":"DeviceDiagnosis"},{"role":"server"},{"supportedFunction":[[{"function":"deviceDiagnosisHeartbeatData"},{"possibleOperations":[{"read":[]}]}],[{"function":"deviceDiagnosisStateData"},{"possibleOperations":[{"read":[]}]}]]},{"description":"Device diagnosis server feature"}]}],[{"description":[{"featureAddress":[{"entity":[4]},{"feature":1}]},{"featureType":"Generic"},{"role":"client"}]}],[{"description":[{"featureAddress":[{"entity":[4]},{"feature":1000}]},{"featureType":"DeviceDiagnosis"},{"role":"server"},{"supportedFunction":[[{"function":"deviceDiagnosisHeartbeatData"},{"possibleOperations":[{"read":[]}]}],[{"function":"deviceDiagnosisStateData"},{"possibleOperations":[{"read":[]}]}]]},{"description":"Device diagnosis server feature"}]}]]}]}]]}]}]}}]}""";
        string LoadControlBindingRequestMessage = """{"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[2]},{"feature":1}]},{"addressDestination":[{"device":"EEBUS_162410145_21821"},{"entity":[0]},{"feature":0}]},{"msgCounter":66237},{"cmdClassifier":"call"},{"ackRequest":true}]},{"payload":[{"cmd":[[{"nodeManagementBindingRequestCall":[{"bindingRequest":[{"clientAddress":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[2]},{"feature":1}]},{"serverAddress":[{"device":"EEBUS_162410145_21821"},{"entity":[1]},{"feature":2}]},{"serverFeatureType":"LoadControl"}]}]}]]}]}]}}]}""";   

        protected override DeviceSettings GetDeviceSettings()
        {
            return Setup.GetCEMSettings().Device;
        }

        [Fact]
        public async Task WHEN_RemoteDeviceHasMultipleGuards_THEN_CEM_SubscribesToDeviceDiagnosisWithLoadControlSubscription()
        {
            var connection = GetDefaultMockConnection();
            SetRemoteDiscoveryData(connection, MultipleEnergyGuardssDetailedDiscoveryData);
            SpineDatagramPayload payload = GetPayload(LoadControlBindingRequestMessage);
            SpineDatagramPayload? answer = await payload.CreateAnswerAsync(DataMessage.NextCount, connection);
            Assert.NotNull(answer);

            AddressType? remoteHeartbeatAddress = connection.GetRemoteHeartbeatAddress(true);
            Assert.NotNull(remoteHeartbeatAddress);
            Assert.True(remoteHeartbeatAddress.entity.SequenceEqual([2]));
        }
    }
}
