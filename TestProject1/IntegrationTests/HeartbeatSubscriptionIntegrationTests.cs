using EEBUS;
using EEBUS.Messages;
using EEBUS.Net;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.StateMachines;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace TestProject1.IntegrationTests
{
    public class HeartbeatSubscriptionIntegrationTests : EebusIntegrationTests
    {
        public HeartbeatSubscriptionIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task WHEN_ControlBoxHasMultipleEnergyGuardEntities_THEN_CEM_SubscribesToDeviceDiagnosisOfBoundEntity()
        {
            ILogger cemLogger = GetLogger("CEM");
            ILogger controlBoxLogger = GetLogger("ControlBox");
            using EEBUSManager cemManager = new EEBUSManager(Setup.GetCEMSettings(), logger: cemLogger);
            using EEBUSManager controlBoxManager = new EEBUSManager(Setup.GetControlBoxWithMultipleCEMEntitiesSettings(), logger: controlBoxLogger);
            string cemSki = cemManager.GetLocalData().SKI;
            string controlBoxSki = controlBoxManager.GetLocalData().SKI;

            await StartAndConnectManagersAsync(cemManager, controlBoxManager);

            Connection? cemConnection = cemManager.GetConnection(controlBoxSki);
            Assert.NotNull(cemConnection);
            Connection? controlBoxConnection = controlBoxManager.GetConnection(cemSki);
            Assert.NotNull(controlBoxConnection);

            Log("Waiting for LoadControl binding request from control box...");
            BindingSubscriptionInfo loadControlBinding = await WaitUntilAsync(
                () => cemConnection.BindingAndSubscriptionManager.GetBindings(BindingSubscriptionDirection.Incoming, "LoadControl").FirstOrDefault(),
                timeoutMs: 10000,
                description: "incoming LoadControl binding on CEM");

            AddressType? remoteHeartbeatAddress = cemConnection.GetRemoteHeartbeatAddress(true);
            Assert.NotNull(remoteHeartbeatAddress);
            Assert.True(remoteHeartbeatAddress.entity.SequenceEqual(loadControlBinding.clientAddress.entity));

            Log("Waiting for DeviceDiagnosis subscription to be established...");
            List<BindingSubscriptionInfo> heartbeatSubscriptions = await WaitUntilAsync(
                () =>
                {
                    var subscriptions = cemConnection.BindingAndSubscriptionManager
                        .GetSubscriptions(BindingSubscriptionDirection.Outgoing)
                        .Where(s => s.serverFeatureType == "DeviceDiagnosis")
                        .ToList();
                    return subscriptions.Count > 0 ? subscriptions : null;
                },
                timeoutMs: 10000,
                description: "outgoing DeviceDiagnosis subscription on CEM");

            BindingSubscriptionInfo heartbeatSubscription = Assert.Single(heartbeatSubscriptions);
            Assert.True(heartbeatSubscription.serverAddress.entity.SequenceEqual(loadControlBinding.clientAddress.entity),
                $"Heartbeat subscription targets entity [{string.Join(",", heartbeatSubscription.serverAddress.entity)}] but LoadControl binding comes from entity [{string.Join(",", loadControlBinding.clientAddress.entity)}]");

            List<BindingSubscriptionInfo> incomingHeartbeatSubscriptions = await WaitUntilAsync(
                () =>
                {
                    var subscriptions = controlBoxConnection.BindingAndSubscriptionManager
                        .GetSubscriptions(BindingSubscriptionDirection.Incoming)
                        .Where(s => s.serverFeatureType == "DeviceDiagnosis")
                        .ToList();
                    return subscriptions.Count > 0 ? subscriptions : null;
                },
                timeoutMs: 10000,
                description: "incoming DeviceDiagnosis subscription on control box");

            BindingSubscriptionInfo incomingHeartbeatSubscription = Assert.Single(incomingHeartbeatSubscriptions);
            Assert.True(incomingHeartbeatSubscription.serverAddress.entity.SequenceEqual(loadControlBinding.clientAddress.entity));
        }
    }
}
