using EEBUS;
using EEBUS.DataStructures;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Data.DataStructures;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using EEBUS.UseCases.ControllableSystem;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TestProject1
{
    public class SpineMessageTests
    {
        private const string TestRemoteSki = "c09ff4c4dc2916414714662366f968f4743af7b7";
        private const string TestLocalSki = "662728a479fa2fcf28e6d9e7855e996ab1d850a2";

        public SpineMessageTests()
        {
            foreach (string ns in new string[] {"EEBUS.SHIP.Messages", "EEBUS.SPINE.Commands", "EEBUS.Entities",
                                                 "EEBUS.UseCases.ControllableSystem", "EEBUS.UseCases.GridConnectionPoint",
                                                 "EEBUS.Features" })
            {
                foreach (Type type in GetTypesInNamespace(typeof(Settings).Assembly, ns))
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
        }

        private static Type[] GetTypesInNamespace(Assembly assembly, string nameSpace)
        {
            return assembly.GetTypes()
                            .Where(t => String.Equals(t.Namespace, nameSpace, StringComparison.Ordinal))
                            .ToArray();
        }


        [Fact]
        public async Task SendMessageTestAsync()
        {
            SpineDatagramPayload payload = GetPayload(EEBusMessages.LoadControl_Write_DeleteTimePeriod_AndUpdate);

            Connection testConnection = GetMockConnection();
            testConnection.BindingAndSubscriptionManager.TryAddOrUpdateClientBinding(payload.datagram.header.addressSource, payload.datagram.header.addressDestination, "LoadControl");

            await payload.EvaluateAsync(testConnection);
        }

        private Connection GetMockConnection()
        {
            var devices = new Devices();
            devices.GetOrCreateLocal(GetSkiBytes(TestLocalSki), GetDeviceSettings());
            var remoteDevice = new RemoteDevice("TestRemote", TestRemoteSki, string.Empty, "TestRemote", default, default);
            devices.Remote.Add(remoteDevice);
            var client = new Client(default, default, devices, remoteDevice);
            SetDiscoveryData(remoteDevice, client);
            return client;
        }

        private byte[] GetSkiBytes(string ski)
        {
            return Enumerable.Range(0, ski.Length / 2)
                                 .Select(x => Convert.ToByte(ski.Substring(x * 2, 2), 16))
                                 .ToArray();
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

        private SpineDatagramPayload GetPayload(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var parsedMessage = ShipMessageBase.Create(bytes) as DataMessage;
            return parsedMessage?.data.payload.Deserialize<SpineDatagramPayload>() ?? throw new Exception("Failed to create payload");
        }

        private void SetDiscoveryData(RemoteDevice device, Connection connection)
        {
            SpineDatagramPayload discoveryPayload = GetPayload(EEBusMessages.MsgNodeManagementDetailedDiscoveryDataReply);
            if (discoveryPayload.datagram == null) throw new Exception("No datagram for message found");

            NodeManagementDetailedDiscoveryData? discoveryData = JsonSerializer.Deserialize<NodeManagementDetailedDiscoveryData>(discoveryPayload.datagram.payload);
            if (discoveryData == null) throw new Exception("Failed to parse discovery data");

            device.SetDiscoveryData(discoveryData, connection);
        }

        #region ElectricalConnectionCharacteristic SendEvent Tests

        private class CapturingNotifyEventHandler(Func<JsonNode?, AddressType, Task> capture) : NotifyEvents
        {
            public Task NotifyAsync(JsonNode? payload, AddressType localFeatureAddress)
                => capture(payload, localFeatureAddress);
        }

        [Fact]
        public async Task SendEventAsync_WithElectricalConnectionServerFeature_SendsNotifyWithCharacteristicData()
        {
            // Arrange
            Connection connection = GetMockConnection();

            JsonNode? capturedPayload = null;
            AddressType capturedAddress = default;
            connection.Local.AddUseCaseEvents(new CapturingNotifyEventHandler(
                (payload, address) => { capturedPayload = payload; capturedAddress = address; return Task.CompletedTask; }
            ));

            // The LPC use case (ControllableSystem) already added a contractualConsumptionNominalMax structure
            ElectricalConnectionCharacteristicDataStructure? dataStructure =
                connection.Local.GetDataStructures<ElectricalConnectionCharacteristicDataStructure>()
                                .FirstOrDefault(ds => ds.CharacteristicType == "contractualConsumptionNominalMax");
            Assert.NotNull(dataStructure);

            // Act
            await dataStructure.SendEventAsync(connection);

            // Assert
            Assert.NotNull(capturedPayload);
            string payloadJson = capturedPayload.ToJsonString();
            Assert.Contains("electricalConnectionCharacteristicListData", payloadJson);
            Assert.Contains("contractualConsumptionNominalMax", payloadJson);
        }

        [Fact]
        public async Task SendEventAsync_PayloadContainsCurrentValue()
        {
            // Arrange
            Connection connection = GetMockConnection();

            JsonNode? capturedPayload = null;
            connection.Local.AddUseCaseEvents(new CapturingNotifyEventHandler(
                (payload, address) => { capturedPayload = payload; return Task.CompletedTask; }
            ));

            ElectricalConnectionCharacteristicDataStructure? dataStructure =
                connection.Local.GetDataStructures<ElectricalConnectionCharacteristicDataStructure>()
                                .FirstOrDefault(ds => ds.CharacteristicType == "contractualConsumptionNominalMax");
            Assert.NotNull(dataStructure);

            // Change the value before sending
            dataStructure.Number = 12345;

            // Act
            await dataStructure.SendEventAsync(connection);

            // Assert - the updated value must appear in the payload
            Assert.NotNull(capturedPayload);
            Assert.Contains("12345", capturedPayload.ToJsonString());
        }

        [Fact]
        public async Task SendEventAsync_WithNoElectricalConnectionFeature_DoesNotThrow()
        {
            // Arrange - create a bare local device without any use cases that register ElectricalConnection-server
            var devices = new Devices();
            devices.GetOrCreateLocal(GetSkiBytes(TestLocalSki), new DeviceSettings()
            {
                Name = "TestDevice",
                Id = "TestDevice",
                Model = "Test",
                Brand = "Test",
                Type = "Inverter",
                Serial = "000",
                Port = 7201,
                Entities = [new EntitySettings { Type = "DeviceInformation" }]
            });
            var remoteDevice = new RemoteDevice("TestRemote", TestRemoteSki, string.Empty, "TestRemote", default, default);
            devices.Remote.Add(remoteDevice);
            Connection connectionWithoutFeature = new Client(default, default, devices, remoteDevice);

            // Manually add a data structure without registering the ElectricalConnection server feature
            var dataStructure = new ElectricalConnectionCharacteristicDataStructure("contractualConsumptionNominalMax", 5000, 0);
            connectionWithoutFeature.Local.Add(dataStructure);

            // Act & Assert - must not throw even when feature address cannot be resolved
            await dataStructure.SendEventAsync(connectionWithoutFeature);
        }

        #endregion

        #region Partial Write Tests

        [Fact]
        public async Task PartialWrite_DeleteTimePeriod_RemovesTimePeriodButKeepsOtherFields()
        {
            // Arrange
            Connection testConnection = GetMockConnection();
            SpineDatagramPayload payload = GetPayload(EEBusMessages.LoadControl_Write_DeleteTimePeriod_AndUpdate);
            testConnection.BindingAndSubscriptionManager.TryAddOrUpdateClientBinding(
                payload.datagram.header.addressSource,
                payload.datagram.header.addressDestination,
                "LoadControl");

            // Get the data structure and set initial values including timePeriod
            LoadControlLimitDataStructure? dataStructure = testConnection.Local
                .GetDataStructures<LoadControlLimitDataStructure>()
                .FirstOrDefault(ds => ds.Id == 0);

            Assert.NotNull(dataStructure);

            // Set initial state with timePeriod
            dataStructure.EndTime = "PT2H";
            dataStructure.LimitActive = true;
            dataStructure.Number = 1000;

            // Act
            await payload.EvaluateAsync(testConnection);

            // Assert - timePeriod should be deleted (null), other fields should be updated
            Assert.Null(dataStructure.EndTime); // timePeriod was deleted by partial write
            Assert.False(dataStructure.LimitActive); // Updated by the write payload
            Assert.Equal(4884, dataStructure.Number); // Updated by the write payload
        }

        [Fact]
        public async Task PartialWrite_UpdateOnly_UpdatesSpecifiedFieldsOnly()
        {
            // Arrange
            Connection testConnection = GetMockConnection();
            SpineDatagramPayload payload = GetPayload(EEBusMessages.LoadControl_Write_UpdateOnly);
            testConnection.BindingAndSubscriptionManager.TryAddOrUpdateClientBinding(
                payload.datagram.header.addressSource,
                payload.datagram.header.addressDestination,
                "LoadControl");

            LoadControlLimitDataStructure? dataStructure = testConnection.Local
                .GetDataStructures<LoadControlLimitDataStructure>()
                .FirstOrDefault(ds => ds.Id == 0);

            
            Assert.NotNull(dataStructure);

            // Set initial state
            dataStructure.EndTime = "PT2H";
            dataStructure.LimitActive = true;
            dataStructure.Number = 1000;

            // Act
            await payload.EvaluateAsync(testConnection);

            // Assert - only specified fields updated, timePeriod stays unchanged (partial write without delete)
            Assert.Equal("PT2H", dataStructure.EndTime); // Partial write preserves fields not in payload
            Assert.False(dataStructure.LimitActive); // Updated
            Assert.Equal(4884, dataStructure.Number); // Updated
        }

        [Fact]
        public async Task PartialWrite_WithDeleteFilter_PreservesNonDeletedFields()
        {
            // Arrange
            Connection testConnection = GetMockConnection();
            SpineDatagramPayload payload = GetPayload(EEBusMessages.LoadControl_Write_DeleteTimePeriod_AndUpdate);
            testConnection.BindingAndSubscriptionManager.TryAddOrUpdateClientBinding(
                payload.datagram.header.addressSource,
                payload.datagram.header.addressDestination,
                "LoadControl");

            LoadControlLimitDataStructure? dataStructure = testConnection.Local
                .GetDataStructures<LoadControlLimitDataStructure>()
                .FirstOrDefault(ds => ds.Id == 0);

            Assert.NotNull(dataStructure);

            // Set initial state
            bool initialLimitChangeable = dataStructure.LimitChangable;
            dataStructure.EndTime = "PT1H";
            dataStructure.LimitActive = true;
            dataStructure.Number = 5000;
            dataStructure.Scale = 0;

            // Act
            await payload.EvaluateAsync(testConnection);

            // Assert - isLimitChangeable should be preserved (not in delete filter or update)
            Assert.Equal(initialLimitChangeable, dataStructure.LimitChangable);
            // Scale should be updated (from payload)
            Assert.Equal(0, dataStructure.Scale);
        }

        [Fact]
        public async Task PartialWrite_WithoutBinding_DoesNotUpdateData()
        {
            // Arrange
            Connection testConnection = GetMockConnection();
            SpineDatagramPayload payload = GetPayload(EEBusMessages.LoadControl_Write_DeleteTimePeriod_AndUpdate);
            // Note: NOT adding binding

            LoadControlLimitDataStructure? dataStructure = testConnection.Local
                .GetDataStructures<LoadControlLimitDataStructure>()
                .FirstOrDefault(ds => ds.Id == 0);

            Assert.NotNull(dataStructure);

            // Set initial state
            dataStructure.EndTime = "PT2H";
            dataStructure.LimitActive = true;
            dataStructure.Number = 1000;

            // Act
            await payload.EvaluateAsync(testConnection);

            // Assert - data should NOT be changed because there's no binding
            Assert.Equal("PT2H", dataStructure.EndTime);
            Assert.True(dataStructure.LimitActive);
            Assert.Equal(1000, dataStructure.Number);
        }

        [Fact]
        public async Task PartialWrite_MultipleUpdates_AppliesAllChanges()
        {
            // Arrange
            Connection testConnection = GetMockConnection();

            // First update - delete timePeriod and update value
            SpineDatagramPayload payload1 = GetPayload(EEBusMessages.LoadControl_Write_DeleteTimePeriod_AndUpdate);
            testConnection.BindingAndSubscriptionManager.TryAddOrUpdateClientBinding(
                payload1.datagram.header.addressSource,
                payload1.datagram.header.addressDestination,
                "LoadControl");

            LoadControlLimitDataStructure? dataStructure = testConnection.Local
                .GetDataStructures<LoadControlLimitDataStructure>()
                .FirstOrDefault(ds => ds.Id == 0);

            Assert.NotNull(dataStructure);

            // Set initial state with timePeriod
            dataStructure.EndTime = "PT2H";
            dataStructure.LimitActive = true;
            dataStructure.Number = 1000;

            // Act - first partial write
            await payload1.EvaluateAsync(testConnection);

            // Assert after first write
            Assert.Null(dataStructure.EndTime);
            Assert.False(dataStructure.LimitActive);
            Assert.Equal(4884, dataStructure.Number);

            // Second update - update only
            SpineDatagramPayload payload2 = GetPayload(EEBusMessages.LoadControl_Write_UpdateOnly);
            await payload2.EvaluateAsync(testConnection);

            // Assert - values should still be correct after second partial write
            Assert.Null(dataStructure.EndTime);
            Assert.False(dataStructure.LimitActive);
            Assert.Equal(4884, dataStructure.Number);
        }

        #endregion
    }
}
