using System.Reflection;
using System.Runtime.CompilerServices;

using EEBUS;
using EEBUS.Models;
using EEBUS.SHIP.Messages;

namespace TestProject1.Ship
{
    /// <summary>
    /// Tests für die SHIP-Prolongation-Logik.
    /// </summary>
    public class ShipProlongationTests
    {
        // ──────────────────────────────────────────────────────────────────────
        // TestClient – erlaubt manuelles Setzen von State/SubState
        // ──────────────────────────────────────────────────────────────────────

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

        // ──────────────────────────────────────────────────────────────────────
        // Konstruktor / Setup
        // ──────────────────────────────────────────────────────────────────────

        public ShipProlongationTests()
        {
            foreach (string ns in new[]
            {
                "EEBUS.SHIP.Messages", "EEBUS.SPINE.Commands",
                "EEBUS.Entities",      "EEBUS.Features",
            })
            {
                foreach (Type t in GetTypesInNamespace(typeof(Settings).Assembly, ns))
                    RuntimeHelpers.RunClassConstructor(t.TypeHandle);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Hilfsmethoden
        // ──────────────────────────────────────────────────────────────────────

        private static Type[] GetTypesInNamespace(Assembly assembly, string ns)
            => assembly.GetTypes()
                       .Where(t => string.Equals(t.Namespace, ns, StringComparison.Ordinal))
                       .ToArray();

        private static byte[] GetSkiBytes(string ski)
            => Enumerable.Range(0, ski.Length / 2)
                         .Select(x => Convert.ToByte(ski.Substring(x * 2, 2), 16))
                         .ToArray();

        private static TestClient CreateTestClient(FakeWebSocket fakeWs)
        {
            var devices = new Devices();
            devices.GetOrCreateLocal(
                GetSkiBytes("662728a479fa2fcf28e6d9e7855e996ab1d850a2"),
                new DeviceSettings
                {
                    Name = "ProlongTest", Id = "Prolong-Test",
                    Model = "Test", Brand = "Test",
                    Type = "EnergyManagementSystem", Serial = "PROLONG001", Port = 7200,
                    Entities = [new EntitySettings { Type = "DeviceInformation" }],
                });
            var remote = new RemoteDevice(
                "TestRemote", "c09ff4c4dc2916414714662366f968f4743af7b7",
                string.Empty, "TestRemote", default, default);
            devices.Remote.Add(remote);
            return new TestClient(fakeWs, devices, remote);
        }

        private static ConnectionHelloMessage MakeProlongationRequest(uint waitingMs = 30_000)
        {
            var msg = new ConnectionHelloMessage(ConnectionHelloPhaseType.pending);
            msg.connectionHello.prolongationRequest = true;
            msg.connectionHello.waiting             = waitingMs;
            return msg;
        }

        private static ConnectionHelloMessage MakeProlongationGrant(uint waitingMs = 30_000)
        {
            var msg = new ConnectionHelloMessage(ConnectionHelloPhaseType.pending);
            msg.connectionHello.prolongationRequest = false;
            msg.connectionHello.waiting             = waitingMs;
            return msg;
        }

        private static ConnectionHelloMessage? ParseHello(byte[] bytes)
            => ConnectionHelloMessage.FromJson(bytes.AsSpan());

        [Fact]
        public async Task Server_ReceivesProlongationRequest_SendsGrantWithProlongationFalse()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.WaitingForConnectionHello);

            await MakeProlongationRequest(30_000).NextServerState(client);

            Assert.Single(fakeWs.SentMessages);
            var sent = ParseHello(fakeWs.SentMessages[0]);
            Assert.NotNull(sent);
            Assert.False(
                sent!.connectionHello.prolongationRequest,
                "Der GRANT darf prolongationRequest nicht auf true setzen.");
        }

        [Fact]
        public async Task Server_ReceivesProlongationRequest_StaysInWaitingForConnectionHello()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.WaitingForConnectionHello);

            var (newState, _) = await MakeProlongationRequest().NextServerState(client);

            Assert.Equal(
                Connection.EState.WaitingForConnectionHello,
                newState);
        }

        [Fact]
        public async Task Server_ReceivesProlongationGrant_StaysInWaitingForConnectionHello()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.WaitingForConnectionHello);

            var (newState, _) = await MakeProlongationGrant(30_000).NextServerState(client);

            Assert.Equal(
                Connection.EState.WaitingForConnectionHello,
                newState);
        }
        
        [Fact]
        public async Task Server_ReceivesProlongationGrant_DoesNotSendAnything()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.WaitingForConnectionHello);

            await MakeProlongationGrant().NextServerState(client);

            Assert.Empty(fakeWs.SentMessages);
        }

        [Fact]
        public async Task Client_ReceivesProlongationRequest_SendsGrantWithProlongationFalse()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.WaitingForConnectionHello);

            try
            {
                await MakeProlongationRequest(30_000).NextClientState(client);

                Assert.Single(fakeWs.SentMessages);
                var sent = ParseHello(fakeWs.SentMessages[0]);
                Assert.NotNull(sent);
                Assert.False(
                    sent!.connectionHello.prolongationRequest,
                    "Der Client-GRANT darf prolongationRequest nicht auf true setzen.");
            }
            catch (Exception ex) when (ex is NullReferenceException or ArgumentNullException)
            {
                Assert.Fail(
                    $"Client.NextClientState wirft {ex.GetType().Name}: Resend() wird auf einer " +
                    "empfangenen Nachricht aufgerufen (sentData==null). " +
                    "Fix: Resend() durch Send() einer neuen ConnectionHelloMessage ersetzen.");
            }
        }

        [Fact]
        public async Task Client_GrantsProlongationTwice_ThenStopsOnThirdRequest()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.WaitingForConnectionHello);

            try
            {
                // 1. Anfrage → GRANT, FirstPending
                var (state1, sub1) = await MakeProlongationRequest().NextClientState(client);
                Assert.Equal(Connection.EState.WaitingForConnectionHello, state1);
                Assert.Equal(Connection.ESubState.FirstPending, sub1);
                client.SetState(state1, sub1);

                // 2. Anfrage → GRANT, SecondPending
                var (state2, sub2) = await MakeProlongationRequest().NextClientState(client);
                Assert.Equal(Connection.EState.WaitingForConnectionHello, state2);
                Assert.Equal(Connection.ESubState.SecondPending, sub2);
                client.SetState(state2, sub2);

                // 3. Anfrage → Stopped (keine weitere Verlängerung)
                var (state3, _) = await MakeProlongationRequest().NextClientState(client);
                Assert.Equal(Connection.EState.Stopped, state3);
            }
            catch (Exception ex) when (ex is NullReferenceException or ArgumentNullException)
            {
                Assert.Fail($"Bug 2 ({ex.GetType().Name}): Bitte zuerst Resend()-Fix anwenden.");
            }
        }

        [Fact]
        public async Task Client_ReceivesProlongationGrant_StaysInWaitingForConnectionHello()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.WaitingForConnectionHello);

            var (newState, _) = await MakeProlongationGrant(30_000).NextClientState(client);

            Assert.Equal(
                Connection.EState.WaitingForConnectionHello,
                newState);
        }
        
        [Fact]
        public async Task Client_ReceivesProlongationGrant_DoesNotSendAnything()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.WaitingForConnectionHello);

            try
            {
                await MakeProlongationGrant().NextClientState(client);
                Assert.Empty(fakeWs.SentMessages);
            }
            catch (Exception ex) when (ex.Message.Contains("Was waiting for Init"))
            {
                Assert.Fail(
                    "Client hat keinen Code-Pfad für pending+prolongationRequest=false. " +
                    "Fix: Neuen Branch in NextClientState für GRANT hinzufügen.");
            }
        }
    }
}
