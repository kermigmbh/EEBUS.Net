using System.Reflection;
using System.Runtime.CompilerServices;

using EEBUS;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.SHIP.Messages;

namespace TestProject1.Ship
{
    /// <summary>
    /// Tests für Reconnect-Verhalten und SKI-basierte Geräteerkennung.
    /// </summary>
    public class ShipReconnectTests
    {
        private const string LocalSki  = "662728a479fa2fcf28e6d9e7855e996ab1d850a2";
        private const string RemoteSki = "c09ff4c4dc2916414714662366f968f4743af7b7";
        private const string RemoteId  = "TestRemote";

        // ── Testhelfer ────────────────────────────────────────────────────────

        private sealed class TestClient : Client
        {
            public TestClient(FakeWebSocket ws, Devices devices, RemoteDevice remote)
                : base(default, ws, devices, remote) { }

            public void SetState(
                Connection.EState    s,
                Connection.ESubState ss = Connection.ESubState.None)
            {
                state    = s;
                subState = ss;
            }
        }

        private sealed class TestServer : Server
        {
            public TestServer(FakeWebSocket ws, Devices devices)
                : base(default, ws, devices) { }

            public void SetState(
                Connection.EState    s,
                Connection.ESubState ss = Connection.ESubState.None)
            {
                state    = s;
                subState = ss;
            }
            
            public RemoteDevice? LookupById(string id) => GetRemote(id);
        }

        // ── Konstruktor: Typregistrierung ─────────────────────────────────────

        public ShipReconnectTests()
        {
            foreach (string ns in new[]
            {
                "EEBUS.SHIP.Messages",
                "EEBUS.SPINE.Commands",
                "EEBUS.Entities",
                "EEBUS.Features",
            })
            {
                foreach (Type t in GetTypesInNamespace(typeof(Settings).Assembly, ns))
                    RuntimeHelpers.RunClassConstructor(t.TypeHandle);
            }
        }

        private static Type[] GetTypesInNamespace(Assembly assembly, string nameSpace)
            => assembly.GetTypes()
                       .Where(t => string.Equals(t.Namespace, nameSpace, StringComparison.Ordinal))
                       .ToArray();

        private static byte[] GetSkiBytes(string ski)
            => Enumerable.Range(0, ski.Length / 2)
                         .Select(x => Convert.ToByte(ski.Substring(x * 2, 2), 16))
                         .ToArray();

        private Devices CreateDevices(bool withRegisteredRemote = true)
        {
            var devices = new Devices();
            devices.GetOrCreateLocal(
                GetSkiBytes(LocalSki),
                new DeviceSettings
                {
                    Name    = "ReconnectTest", Id     = "Reconnect-Test",
                    Model   = "Test",          Brand  = "Test",
                    Type    = "EnergyManagementSystem",
                    Serial  = "RC001",         Port   = 7300,
                    Entities = [new EntitySettings { Type = "DeviceInformation" }],
                });

            if (withRegisteredRemote)
            {
                devices.Remote.Add(
                    new RemoteDevice(RemoteId, RemoteSki, string.Empty, "TestRemote",
                                     default, default));
            }

            return devices;
        }

        [Fact]
        public void Ski_FromHexString_RoundTripsToString()
        {
            var ski = new SKI(LocalSki);
            Assert.Equal(LocalSki, ski.ToString());
        }

        [Fact]
        public void Ski_EqualityOperator_TrueForIdenticalBytes()
        {
            var a = new SKI(LocalSki);
            var b = new SKI(LocalSki);
            Assert.True(a == b,  "Gleiche Bytes müssen == ergeben.");
            Assert.False(a != b, "Gleiche Bytes dürfen != nicht ergeben.");
        }

        [Fact]
        public void Ski_InequalityOperator_TrueForDifferentBytes()
        {
            var a = new SKI(LocalSki);
            var b = new SKI(RemoteSki);
            Assert.True(a != b, "Verschiedene Bytes müssen != ergeben.");
        }

        public void Devices_GetOrCreateRemote_RejectsSelfSki()
        {
            var devices = CreateDevices(withRegisteredRemote: false);
            var result  = devices.GetOrCreateRemote("self", LocalSki, string.Empty, "Self");
            Assert.Null(result);
        }

        [Fact]
        public void Devices_GetOrCreateRemote_ReusesByDeviceId()
        {
            var devices = CreateDevices(withRegisteredRemote: false);
            var first   = devices.GetOrCreateRemote(RemoteId, RemoteSki, string.Empty, "R");
            var second  = devices.GetOrCreateRemote(RemoteId, RemoteSki, string.Empty, "R");

            Assert.Same(first, second);
            Assert.Equal(1, devices.Remote.Count);
        }

        [Fact]
        public void Client_Reconnect_DevicesRemoteIsUnchangedAfterReconnect()
        {
            var devices        = CreateDevices();
            var originalRemote = devices.Remote[0];

            // Erste Verbindung aufbauen und wieder trennen
            var ws1     = new FakeWebSocket();
            var client1 = new TestClient(ws1, devices, originalRemote);
            client1.SetState(Connection.EState.Connected);

            // Reconnect: neues FakeWebSocket, aber dasselbe RemoteDevice-Objekt
            var ws2     = new FakeWebSocket();
            var client2 = new TestClient(ws2, devices, originalRemote);
            client2.SetState(Connection.EState.Connected);

            Assert.Equal(1, devices.Remote.Count);
            Assert.Same(originalRemote, devices.Remote[0]);
        }

        [Fact]
        public void Server_Reconnect_SharedDevicesProvidesSameRemoteOnBothConnections()
        {
            var devices = CreateDevices();

            // Erste Server-Instanz (erste Verbindung)
            var server1 = new TestServer(new FakeWebSocket(), devices);
            var found1  = server1.LookupById(RemoteId);

            // Zweite Server-Instanz (Reconnect) – gleicher Devices-Container
            var server2 = new TestServer(new FakeWebSocket(), devices);
            var found2  = server2.LookupById(RemoteId);

            Assert.NotNull(found1);
            Assert.NotNull(found2);
            Assert.Same(found1, found2);      // selbes Objekt, keine Kopie
            Assert.Equal(1, devices.Remote.Count);
        }

        [Fact]
        public async Task Server_ReceivesAccessMethodsFromUnregisteredDevice_ShouldStopConnection()
        {
            // Devices enthält kein einziges Remote-Gerät – komplett leerer Trust-Store.
            var devices = CreateDevices(withRegisteredRemote: false);

            var server = new TestServer(new FakeWebSocket(), devices);
            server.SetState(Connection.EState.WaitingForAccessMethods);

            var accessMethods   = new AccessMethodsMessage("completely-unknown-device-id");
            var (newState, _)   = await accessMethods.NextServerState(server);

            Assert.Equal(Connection.EState.Stopped, newState);
        }
    }
}
