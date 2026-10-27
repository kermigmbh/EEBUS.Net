using EEBUS;
using EEBUS.SHIP.Messages;

namespace TestProject1.Ship
{
    /// <summary>
    /// Tests für die SHIP-Verbindungsbeendigung (SHIP 13.4.8).
    /// </summary>
    public class ShipCloseMessageTests : ShipTestBase
    {
        private static CloseMessage? ParseClose(byte[] sentBytes)
            => CloseMessage.FromJson(sentBytes.AsSpan());

        [Fact]
        public void Announce_ContainsMaxTime_Confirm_DoesNot()
        {
            var announce = new CloseMessage(ConnectionClosePhaseType.announce);
            var confirm = new CloseMessage(ConnectionClosePhaseType.confirm);

            Assert.Equal(CloseMessage.DefaultMaxTimeMs, announce.connectionClose.maxTime);
            Assert.Null(confirm.connectionClose.maxTime);
            Assert.DoesNotContain("maxTime", confirm.ToEEBUSJson());
            Assert.DoesNotContain("reason", confirm.ToEEBUSJson());
        }

        [Fact]
        public async Task Client_ReceivesAnnounce_SendsConfirm_AndStops()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.Connected);

            var announce = new CloseMessage(ConnectionClosePhaseType.announce);
            announce.connectionClose.reason = ConnectionCloseReasonType.unspecific;

            var (newState, _) = await announce.NextClientStateAsync(client);

            Assert.Equal(Connection.EState.Stopped, newState);
            var sent = Assert.Single(fakeWs.SentMessages);
            var confirm = ParseClose(sent);
            Assert.NotNull(confirm);
            Assert.Equal(ConnectionClosePhaseType.confirm, confirm!.connectionClose.phase);
            Assert.Null(confirm.connectionClose.maxTime);
            Assert.Null(confirm.connectionClose.reason);
        }

        [Fact]
        public async Task Server_ReceivesAnnounce_SendsConfirm_AndClosesWebSocket()
        {
            var fakeWs = new FakeWebSocket();
            var server = new TestServer(fakeWs, CreateDevices());
            server.SetState(Connection.EState.Connected);

            var announce = new CloseMessage(ConnectionClosePhaseType.announce);

            var (newState, _) = await announce.NextServerStateAsync(server);

            Assert.Equal(Connection.EState.Stopped, newState);
            var sent = Assert.Single(fakeWs.SentMessages);
            Assert.Equal(ConnectionClosePhaseType.confirm, ParseClose(sent)!.connectionClose.phase);
            Assert.Equal(System.Net.WebSockets.WebSocketState.CloseSent, fakeWs.State);
        }

        [Fact]
        public async Task Client_ReceivesConfirm_DoesNotAnswer_AndDisconnects()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.WaitingForCloseConfirm);

            var (newState, _) = await new CloseMessage(ConnectionClosePhaseType.confirm).NextClientStateAsync(client);

            Assert.Equal(Connection.EState.Disconnected, newState);
            Assert.Empty(fakeWs.SentMessages);
        }

        [Fact]
        public async Task PushCloseMessage_ReceivesConfirm_ReturnsConfirmBeforeMaxTime()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.Connected);

            var announce = new CloseMessage(ConnectionClosePhaseType.announce);
            Task<CloseMessage?> pushTask = client.PushCloseMessageAsync(announce);

            Assert.Equal(Connection.EState.WaitingForCloseConfirm, client.State);
            Assert.Single(fakeWs.SentMessages);

            // Peer answers with the confirm
            var confirm = new CloseMessage(ConnectionClosePhaseType.confirm);
            await confirm.NextClientStateAsync(client);
            client.CompletePendingRequest(confirm.GetId(), confirm);

            CloseMessage? result = await pushTask.WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.NotNull(result);
            Assert.Equal(ConnectionClosePhaseType.confirm, result!.connectionClose.phase);
            Assert.Equal(Connection.EState.Disconnected, client.State);
        }

        [Fact]
        public async Task PushCloseMessage_WithoutConfirm_ReturnsNullAfterMaxTime()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.Connected);

            var announce = new CloseMessage(ConnectionClosePhaseType.announce);
            announce.connectionClose.maxTime = 100;

            CloseMessage? result = await client.PushCloseMessageAsync(announce);

            Assert.Null(result);
            Assert.Equal(Connection.EState.Disconnected, client.State);
        }

        [Fact]
        public async Task SimultaneousClose_ReceivedAnnounceWhileWaitingForConfirm_CompletesOwnClose()
        {
            var fakeWs = new FakeWebSocket();
            var client = CreateTestClient(fakeWs);
            client.SetState(Connection.EState.Connected);

            var ownAnnounce = new CloseMessage(ConnectionClosePhaseType.announce);
            Task<CloseMessage?> pushTask = client.PushCloseMessageAsync(ownAnnounce);
            Assert.Equal(Connection.EState.WaitingForCloseConfirm, client.State);

            // Peer announces at the same time (13.4.8.1.2 general rule 1)
            var peerAnnounce = new CloseMessage(ConnectionClosePhaseType.announce);
            await peerAnnounce.NextClientStateAsync(client);

            CloseMessage? result = await pushTask.WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.NotNull(result);
            Assert.Equal(ConnectionClosePhaseType.announce, result!.connectionClose.phase);

            // Our announce + our confirm for the peer's announce
            Assert.Equal(2, fakeWs.SentMessages.Count);
            Assert.Equal(ConnectionClosePhaseType.announce, ParseClose(fakeWs.SentMessages[0])!.connectionClose.phase);
            Assert.Equal(ConnectionClosePhaseType.confirm, ParseClose(fakeWs.SentMessages[1])!.connectionClose.phase);
        }
    }
}
