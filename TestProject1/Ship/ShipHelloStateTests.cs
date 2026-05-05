using System.Reflection;
using System.Runtime.CompilerServices;

using EEBUS;
using EEBUS.Enums;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.SHIP.Messages;

namespace TestProject1.Ship
{
    /// <summary>
    /// Tests für die SHIP-Hello-Phase und das Timeout-Verhalten.
    /// </summary>
    public class ShipHelloStateTests
    {
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

            public FakeWebSocket FakeWs => (FakeWebSocket)WebSocket;
        }

        public ShipHelloStateTests()
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

        private static TestClient CreateTestClient(FakeWebSocket fakeWs)
        {
            var devices = new Devices();
            devices.GetOrCreateLocal(
                GetSkiBytes("662728a479fa2fcf28e6d9e7855e996ab1d850a2"),
                new DeviceSettings
                {
                    Name = "ShipTest", Id = "Ship-Test",
                    Model = "Test", Brand = "Test",
                    Type = "EnergyManagementSystem", Serial = "SHIP001", Port = 7200,
                    Entities = [new EntitySettings { Type = "DeviceInformation" }],
                });
            var remote = new RemoteDevice(
                "TestRemote", "c09ff4c4dc2916414714662366f968f4743af7b7",
                string.Empty, "TestRemote", default, default);
            devices.Remote.Add(remote);
            return new TestClient(fakeWs, devices, remote);
        }

        private static ConnectionHelloMessage? ParseHello(byte[] sentBytes)
            => ConnectionHelloMessage.FromJson(sentBytes.AsSpan());

        [Fact]
        public void InitMessage_ServerTest_WithValidCmiHead_ReturnsNoError()
        {
            var msg = new InitMessage();
            // bytes sind schon korrekt initialisiert: { INIT, CMI_HEAD }
            var (_, _, error) = msg.ServerTest(Connection.EState.Disconnected);
            Assert.Null(error);
        }

        [Fact]
        public void InitMessage_ServerTest_WithInvalidPayload_ReturnsStoppedAndError()
        {
            var msg = new InitMessage { bytes = new byte[] { SHIPMessageType.INIT, 0xFF } };
            var (newState, _, error) = msg.ServerTest(Connection.EState.Disconnected);

            Assert.Equal(Connection.EState.Stopped, newState);
            Assert.NotNull(error);
        }

        [Fact]
        public void InitMessage_ClientTest_WithValidCmiHead_ReturnsNoError()
        {
            var msg = new InitMessage();
            var (_, _, error) = msg.ClientTest(Connection.EState.Disconnected);
            Assert.Null(error);
        }

        [Fact]
        public async Task Server_ReceivesInit_TransitionsTo_WaitingForConnectionHello()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.Disconnected);

            var initMsg = new InitMessage();
            var (newState, newSubState) = await initMsg.NextServerState(client);

            Assert.Equal(Connection.EState.WaitingForConnectionHello, newState);
            Assert.Equal(Connection.ESubState.None, newSubState);
            // Server echot das INIT zurück
            Assert.Single(fakeWs.SentMessages);
        }

        [Fact]
        public async Task Server_ReceivesHelloReady_TransitionsTo_WaitingForProtocolHandshake()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.WaitingForConnectionHello);

            var hello = new ConnectionHelloMessage(ConnectionHelloPhaseType.ready);
            var (newState, newSubState) = await hello.NextServerState(client);

            Assert.Equal(Connection.EState.WaitingForProtocolHandshake, newState);
            Assert.Equal(Connection.ESubState.None, newSubState);

            // Server echot das Hello{ready} zurück
            Assert.Single(fakeWs.SentMessages);
            var sentHello = ParseHello(fakeWs.SentMessages[0]);
            Assert.NotNull(sentHello);
            Assert.Equal(ConnectionHelloPhaseType.ready, sentHello!.connectionHello.phase);
        }

        [Fact]
        public async Task Server_ReceivesHelloAborted_TransitionsTo_Stopped()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.WaitingForConnectionHello);

            var hello = new ConnectionHelloMessage(ConnectionHelloPhaseType.aborted);
            var (newState, _) = await hello.NextServerState(client);

            Assert.Equal(Connection.EState.Stopped, newState);
            Assert.Empty(fakeWs.SentMessages);
        }

        [Fact]
        public async Task Client_ReceivesInit_TransitionsTo_WaitingForConnectionHello()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.Disconnected);

            var initMsg = new InitMessage();
            var (newState, newSubState) = await initMsg.NextClientState(client);

            Assert.Equal(Connection.EState.WaitingForConnectionHello, newState);
            Assert.Equal(Connection.ESubState.None, newSubState);

            // Client schickt Hello{ready} als Antwort
            Assert.Single(fakeWs.SentMessages);
            var sentHello = ParseHello(fakeWs.SentMessages[0]);
            Assert.NotNull(sentHello);
            Assert.Equal(ConnectionHelloPhaseType.ready, sentHello!.connectionHello.phase);
        }

        [Fact]
        public async Task Client_ReceivesHelloReady_TransitionsTo_WaitingForProtocolHandshake()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.WaitingForConnectionHello);

            var hello = new ConnectionHelloMessage(ConnectionHelloPhaseType.ready);
            var (newState, newSubState) = await hello.NextClientState(client);

            Assert.Equal(Connection.EState.WaitingForProtocolHandshake, newState);
            Assert.Equal(Connection.ESubState.None, newSubState);

            // Client sendet ProtocolHandshake{announceMax}
            Assert.Single(fakeWs.SentMessages);
            var sentHandshake = ProtocolHandshakeMessage.FromJson(fakeWs.SentMessages[0].AsSpan());
            Assert.NotNull(sentHandshake);
            Assert.Equal(ProtocolHandshakeTypeType.announceMax,
                         sentHandshake!.messageProtocolHandshake.handshakeType);
        }

        [Fact]
        public async Task Client_ReceivesHelloAborted_TransitionsTo_Stopped()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.WaitingForConnectionHello);

            var hello = new ConnectionHelloMessage(ConnectionHelloPhaseType.aborted);
            var (newState, _) = await hello.NextClientState(client);

            Assert.Equal(Connection.EState.Stopped, newState);
            Assert.Empty(fakeWs.SentMessages);
        }

        [Fact]
        public async Task Connection_OnReceiveTimeout_StateShouldBeErrorOrTimeout()
        {
            var cancellingWs = new CancellingWebSocket();
            var client       = CreateTestClient(cancellingWs);

            // Starte den Client-Loop im Hintergrund
            await client.Run();

            // Warten bis der Hintergrund-Task fertig ist (ReceiveAsync wirft sofort)
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            while (client.State != Connection.EState.Stopped        &&
                   client.State != Connection.EState.ErrorOrTimeout &&
                   !timeout.IsCancellationRequested)
            {
                await Task.Delay(10, CancellationToken.None);
            }

            // Nach einem Timeout soll der Zustand ErrorOrTimeout sein,
            // nicht Disconnected (Initialwert, niemals geändert in catch).
            Assert.Equal(
                Connection.EState.ErrorOrTimeout,
                client.State); 
        }
    }
}
