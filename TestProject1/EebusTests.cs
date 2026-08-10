using EEBUS;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TestProject1
{
    public abstract class EebusTests
    {
        protected const string DefaultRemoteSki = "c09ff4c4dc2916414714662366f968f4743af7b7";
        protected const string DefaultLocalSki = "662728a479fa2fcf28e6d9e7855e996ab1d850a2";

        public EebusTests()
        {
            foreach (string ns in new string[] {"EEBUS.SHIP.Messages", "EEBUS.SPINE.Commands", "EEBUS.Entities",
                                                 "EEBUS.UseCases.ControllableSystem", "EEBUS.UseCases.EnergyGuard", "EEBUS.UseCases.GridConnectionPoint", "EEBUS.UseCases.MonitoringAppliance",
                                                 "EEBUS.UseCases.MonitoredUnit", "EEBUS.Features" })
            {
                foreach (Type type in GetTypesInNamespace(typeof(Settings).Assembly, ns))
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
        }

        private Type[] GetTypesInNamespace(Assembly assembly, string nameSpace)
        {
            return assembly.GetTypes()
                            .Where(t => String.Equals(t.Namespace, nameSpace, StringComparison.Ordinal))
                            .ToArray();
        }

        protected byte[] GetSkiBytes(string ski)
        {
            return Enumerable.Range(0, ski.Length / 2)
                                 .Select(x => Convert.ToByte(ski.Substring(x * 2, 2), 16))
                                 .ToArray();
        }

        protected SpineDatagramPayload GetPayload(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var parsedMessage = ShipMessageBase.Create(bytes) as DataMessage;
            if (parsedMessage == null) throw new Exception("Failed to parse message");
            SpineDatagramPayload? payload = JsonHelper.FromJsonNode<SpineDatagramPayload>(parsedMessage.data.payload);
            if (payload == null) throw new Exception("Failed to create payload");

            return payload;
        }

        protected void SetDiscoveryData(Connection connection, string discoveryMessage)
        {
            if (connection.Remote == null) return;

            SpineDatagramPayload discoveryPayload = GetPayload(discoveryMessage);
            if (discoveryPayload.datagram == null) throw new Exception("No datagram for message found");

            NodeManagementDetailedDiscoveryData? discoveryData = JsonSerializer.Deserialize<NodeManagementDetailedDiscoveryData>(discoveryPayload.datagram.payload);
            if (discoveryData == null) throw new Exception("Failed to parse discovery data");

            connection.Remote.SetDiscoveryData(discoveryData, connection);
        }

        protected void SetUseCaseData(Connection connection, string useCaseMessage)
        {
            if (connection.Remote == null) return;

            SpineDatagramPayload discoveryPayload = GetPayload(useCaseMessage);
            if (discoveryPayload.datagram == null) throw new Exception("No datagram for message found");

            NodeManagementUseCaseData? useCaseData = JsonSerializer.Deserialize<NodeManagementUseCaseData>(discoveryPayload.datagram.payload);
            if (useCaseData == null) throw new Exception("Failed to parse discovery data");

            connection.Remote.SetUseCaseData(useCaseData);
        }

        protected Connection GetDefaultMockConnection()
        {
            return GetMockConnection(DefaultLocalSki, DefaultRemoteSki);
        }

        protected Connection GetMockConnection(string localSki, string remoteSki, string remoteName = "TestRemote")
        {
            var devices = new Devices();
            devices.GetOrCreateLocal(GetSkiBytes(localSki), GetDeviceSettings());
            var remoteDevice = devices.GetOrCreateRemote(remoteName, remoteSki, string.Empty, remoteName);
            var client = new Client(default, default, devices, remoteDevice);
            return client;
        }

        protected abstract DeviceSettings GetDeviceSettings();
    }
}
