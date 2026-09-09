using EEBUS;
using EEBUS.Messages;
using EEBUS.Net;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;

namespace TestProject1.ConsistencyTests
{
    public class BindingAndScubscriptionTests : EebusTests
    {
        const string RemoteDeviceId = "TestRemoteDevice";

        [Fact]
        public async Task Subscription_IsAdded_AndRemoved_ViaNodeManagementCalls()
        {
            Connection connection = GetDefaultMockConnection();
            SetRemoteDiscoveryData(connection);
            Assert.NotNull(connection.Remote);

            const string serverFeatureType = "LoadControl";
            AddressType clientAddress = new AddressType { device = connection.Remote.DeviceId, entity = [1], feature = 1 };
            AddressType? serverAddress = connection.Local.GetFeatureAddress(serverFeatureType, server: true);
            Assert.NotNull(serverAddress);

            // Subscribe
            SpineDatagramPayload subscriptionRequest = DataMessage.CreateSubscription(clientAddress, serverAddress, serverFeatureType, connection.Remote.DeviceId, connection.Local.DeviceId).SpineDatagramPayload;
            SpineDatagramPayload? subscriptionAnswer = await subscriptionRequest.CreateAnswerAsync(DataMessage.NextCount, connection);
            Assert.NotNull(subscriptionAnswer);
            Assert.Equal("result", subscriptionAnswer.datagram.header.cmdClassifier);

            // Read subscription data -> subscription must be present
            List<NodeManagementSubscriptionEntryDataType> entries = await ReadSubscriptionEntriesAsync(connection, clientAddress);
            Assert.Single(entries);
            Assert.Equal(clientAddress, entries[0].clientAddress);
            Assert.Equal(serverAddress, entries[0].serverAddress);

            // Delete subscription
            SpineDatagramPayload deleteRequest = DataMessage.CreateSubscriptionDelete(clientAddress, serverAddress, connection.Remote.DeviceId, connection.Local.DeviceId).SpineDatagramPayload;
            SpineDatagramPayload? deleteAnswer = await deleteRequest.CreateAnswerAsync(DataMessage.NextCount, connection);
            Assert.NotNull(deleteAnswer);
            Assert.Equal("result", deleteAnswer.datagram.header.cmdClassifier);

            // Read subscription data -> subscription must be gone
            entries = await ReadSubscriptionEntriesAsync(connection, clientAddress);
            Assert.Empty(entries);
        }

        [Fact]
        public async Task Binding_IsAdded_AndRemoved_ViaNodeManagementCalls()
        {
            Connection connection = GetDefaultMockConnection();
            SetRemoteDiscoveryData(connection);
            Assert.NotNull(connection.Remote);

            const string serverFeatureType = "LoadControl";
            AddressType clientAddress = new AddressType { device = connection.Remote.DeviceId, entity = [1], feature = 1 };
            AddressType? serverAddress = connection.Local.GetFeatureAddress(serverFeatureType, server: true);
            Assert.NotNull(serverAddress);

            // Bind
            SpineDatagramPayload bindingRequest = DataMessage.CreateBinding(clientAddress, serverAddress, serverFeatureType, connection.Remote.DeviceId, connection.Local.DeviceId).SpineDatagramPayload;
            SpineDatagramPayload? bindingAnswer = await bindingRequest.CreateAnswerAsync(DataMessage.NextCount, connection);
            Assert.NotNull(bindingAnswer);
            Assert.Equal("result", bindingAnswer.datagram.header.cmdClassifier);

            // Read binding data -> binding must be present
            List<NodeManagementBindingEntryDataType> entries = await ReadBindingEntriesAsync(connection, clientAddress);
            Assert.Single(entries);
            Assert.Equal(clientAddress, entries[0].clientAddress);
            Assert.Equal(serverAddress, entries[0].serverAddress);

            // Delete binding
            SpineDatagramPayload deleteRequest = DataMessage.CreateBindingDelete(clientAddress, serverAddress, connection.Remote.DeviceId, connection.Local.DeviceId).SpineDatagramPayload;
            SpineDatagramPayload? deleteAnswer = await deleteRequest.CreateAnswerAsync(DataMessage.NextCount, connection);
            Assert.NotNull(deleteAnswer);
            Assert.Equal("result", deleteAnswer.datagram.header.cmdClassifier);

            // Read binding data -> binding must be gone
            entries = await ReadBindingEntriesAsync(connection, clientAddress);
            Assert.Empty(entries);
        }

        private async Task<List<NodeManagementBindingEntryDataType>> ReadBindingEntriesAsync(Connection connection, AddressType clientAddress)
        {
            AddressType nodeManagementAddress = new AddressType { device = connection.Local.DeviceId, entity = [0], feature = 0 };
            SpineDatagramPayload readRequest = DataMessage.CreateRead(clientAddress, nodeManagementAddress, new NodeManagementBindingData()).SpineDatagramPayload;
            SpineDatagramPayload? answer = await readRequest.CreateAnswerAsync(DataMessage.NextCount, connection);
            Assert.NotNull(answer);
            Assert.Equal("reply", answer.datagram.header.cmdClassifier);
            Assert.NotNull(answer.datagram.payload);

            NodeManagementBindingData? data = JsonHelper.FromJsonNode<NodeManagementBindingData>(answer.datagram.payload);
            Assert.NotNull(data);
            Assert.Single(data.cmd);

            return data.cmd[0].nodeManagementBindingData.bindingEntry;
        }

        private async Task<List<NodeManagementSubscriptionEntryDataType>> ReadSubscriptionEntriesAsync(Connection connection, AddressType clientAddress)
        {
            AddressType nodeManagementAddress = new AddressType { device = connection.Local.DeviceId, entity = [0], feature = 0 };
            SpineDatagramPayload readRequest = DataMessage.CreateRead(clientAddress, nodeManagementAddress, new NodeManagementSubscriptionData()).SpineDatagramPayload;
            SpineDatagramPayload? answer = await readRequest.CreateAnswerAsync(DataMessage.NextCount, connection);
            Assert.NotNull(answer);
            Assert.Equal("reply", answer.datagram.header.cmdClassifier);
            Assert.NotNull(answer.datagram.payload);

            NodeManagementSubscriptionData? data = JsonHelper.FromJsonNode<NodeManagementSubscriptionData>(answer.datagram.payload);
            Assert.NotNull(data);
            Assert.Single(data.cmd);

            return data.cmd[0].nodeManagementSubscriptionData.subscriptionEntry;
        }

        private void SetRemoteDiscoveryData(Connection connection)
        {
            NodeManagementDetailedDiscoveryData discoveryData = new NodeManagementDetailedDiscoveryData();
            discoveryData.cmd[0].nodeManagementDetailedDiscoveryData = new NodeManagementDetailedDiscoveryDataType
            {
                specificationVersionList = new SpecificationVersionListType(),
                deviceInformation = new DeviceInformationType
                {
                    description = new DeviceInformationDescriptionType
                    {
                        deviceAddress = new DeviceAddressType { device = RemoteDeviceId },
                        deviceType = "Generic",
                        networkFeatureSet = "smart"
                    }
                },
                entityInformation =
                [
                    new EntityInformationType { description = new EntityInformationDescriptionType { entityAddress = new EntityAddressType { device = RemoteDeviceId, entity = [0] }, entityType = "DeviceInformation" } },
                    new EntityInformationType { description = new EntityInformationDescriptionType { entityAddress = new EntityAddressType { device = RemoteDeviceId, entity = [1] }, entityType = "CEM" } }
                ],
                featureInformation =
                [
                    new FeatureInformationType { description = new FeatureInformationDescriptionType { featureAddress = new FeatureAddressType { device = RemoteDeviceId, entity = [0], feature = 0 }, featureType = "NodeManagement", role = "special" } },
                    new FeatureInformationType { description = new FeatureInformationDescriptionType { featureAddress = new FeatureAddressType { device = RemoteDeviceId, entity = [1], feature = 1 }, featureType = "Generic", role = "client" } }
                ]
            };

            connection.Remote!.SetDiscoveryData(discoveryData, connection);
        }

        protected override DeviceSettings GetDeviceSettings()
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
                           }
                       ]}
                ]
            };
        }

        
    }
}
