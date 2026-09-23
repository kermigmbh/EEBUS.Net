using EEBUS;
using EEBUS.Models;
using EEBUS.SHIP.Messages;

namespace TestProject1.Ship
{
    /// <summary>
    /// Tests für Reconnect-Verhalten und SKI-basierte Geräteerkennung.
    /// </summary>
    public class ShipReconnectTests : ShipTestBase
    {
        [Fact]
        public void Ski_FromHexString_RoundTripsToString()
        {
            var ski = new SKI(DefaultLocalSki);
            Assert.Equal(DefaultLocalSki, ski.ToString());
        }

        [Fact]
        public void Ski_EqualityOperator_TrueForIdenticalBytes()
        {
            var a = new SKI(DefaultLocalSki);
            var b = new SKI(DefaultLocalSki);
            Assert.True(a == b,  "Gleiche Bytes müssen == ergeben.");
            Assert.False(a != b, "Gleiche Bytes dürfen != nicht ergeben.");
        }

        [Fact]
        public void Ski_InequalityOperator_TrueForDifferentBytes()
        {
            var a = new SKI(DefaultLocalSki);
            var b = new SKI(DefaultRemoteSki);
            Assert.True(a != b, "Verschiedene Bytes müssen != ergeben.");
        }
        
        [Fact]
        public void Devices_GetOrCreateRemote_RejectsSelfSki()
        {
            var devices = CreateDevices(withRegisteredRemote: false);
            var result  = devices.GetOrCreateRemote("self", DefaultLocalSki, string.Empty, "Self");
            Assert.Null(result);
        }

        [Fact]
        public void Devices_GetOrCreateRemote_ReusesByDeviceId()
        {
            var devices = CreateDevices(withRegisteredRemote: false);
            var first   = devices.GetOrCreateRemote(DefaultRemoteId, DefaultRemoteSki, string.Empty, "R");
            var second  = devices.GetOrCreateRemote(DefaultRemoteId, DefaultRemoteSki, string.Empty, "R");

            Assert.Same(first, second);
            Assert.Single(devices.GetRemotes());
        }

        [Fact]
        public void Client_Reconnect_DevicesRemoteIsUnchangedAfterReconnect()
        {
            var devices        = CreateDevices();
            var originalRemote = devices.GetRemotes()[0];

            // Erste Verbindung aufbauen und wieder trennen
            var ws1     = new FakeWebSocket();
            var client1 = new TestClient(ws1, devices, originalRemote);
            client1.SetState(Connection.EState.Connected);

            // Reconnect: neues FakeWebSocket, aber dasselbe RemoteDevice-Objekt
            var ws2     = new FakeWebSocket();
            var client2 = new TestClient(ws2, devices, originalRemote);
            client2.SetState(Connection.EState.Connected);

            Assert.Single(devices.GetRemotes());
            Assert.Same(originalRemote, devices.GetRemotes()[0]);
        }

        [Fact]
        public void Server_Reconnect_SharedDevicesProvidesSameRemoteOnBothConnections()
        {
            var devices = CreateDevices();

            // Erste Server-Instanz (erste Verbindung)
            var server1 = new TestServer(new FakeWebSocket(), devices);
            var found1  = server1.LookupById(DefaultRemoteId);

            // Zweite Server-Instanz (Reconnect) – gleicher Devices-Container
            var server2 = new TestServer(new FakeWebSocket(), devices);
            var found2  = server2.LookupById(DefaultRemoteId);

            Assert.NotNull(found1);
            Assert.NotNull(found2);
            Assert.Same(found1, found2);      // selbes Objekt, keine Kopie
            Assert.Single(devices.GetRemotes());
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
