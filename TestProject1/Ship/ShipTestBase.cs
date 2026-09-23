using EEBUS;
using EEBUS.Models;

namespace TestProject1.Ship
{
    /// <summary>
    /// Gemeinsame Basis für SHIP-Tests: sorgt über <see cref="EebusTests"/> für die
    /// Registrierung der statischen Entity-/Feature-/Message-Klassen und stellt
    /// Test-Doubles sowie Factory-Methoden für Devices, Client und Server bereit.
    /// </summary>
    public abstract class ShipTestBase : EebusTests
    {
        protected const string DefaultRemoteId = "TestRemote";

        // ────────────────────────────────────────────────────────────────────────
        // Test-Doubles – erlauben manuelles Setzen von State/SubState
        // ────────────────────────────────────────────────────────────────────────

        internal sealed class TestClient : Client
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

            public FakeWebSocket FakeWs => (FakeWebSocket)WebSocket;
        }

        internal sealed class TestServer : Server
        {
            public TestServer(FakeWebSocket ws, Devices devices)
                : base(string.Empty, default, ws, devices) { }

            public void SetState(
                Connection.EState    s,
                Connection.ESubState ss = Connection.ESubState.None)
            {
                state    = s;
                subState = ss;
            }

            public RemoteDevice? LookupById(string id) => GetRemote(id);
        }

        // ────────────────────────────────────────────────────────────────────────
        // Hilfsmethoden
        // ────────────────────────────────────────────────────────────────────────

        protected override DeviceSettings GetDeviceSettings()
            => new DeviceSettings
            {
                Name = "ShipTest", Id = "Ship-Test",
                Model = "Test", Brand = "Test",
                Type = "EnergyManagementSystem", Serial = "SHIP001", Port = 7200,
                Entities = [new EntitySettings { Type = "DeviceInformation" }],
            };

        protected Devices CreateDevices(bool withRegisteredRemote = true)
        {
            var devices = new Devices();
            devices.GetOrCreateLocal(GetSkiBytes(DefaultLocalSki), GetDeviceSettings());

            if (withRegisteredRemote)
            {
                devices.GetOrCreateRemote(DefaultRemoteId, DefaultRemoteSki, string.Empty, DefaultRemoteId);
            }

            return devices;
        }

        internal TestClient CreateTestClient(FakeWebSocket fakeWs)
        {
            var devices = CreateDevices();
            var remote  = devices.GetRemotes()[0];
            return new TestClient(fakeWs, devices, remote);
        }
    }
}
