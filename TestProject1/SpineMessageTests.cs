using EEBUS;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

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
                                    Duration = 7200,
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
                                    Duration = 7200,
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
    }
}
