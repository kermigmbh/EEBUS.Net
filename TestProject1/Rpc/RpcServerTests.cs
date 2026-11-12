using EEBUS.Net.Rpc;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace TestProject1.Rpc
{
    public class RpcServerTests
    {
        private static async Task<RpcServer> StartServerAsync()
        {
            RpcServer server = new();
            await server.StartAsync(0);
            return server;
        }

        private static async Task AssertConnectionRefusedAsync(int port)
        {
            await Assert.ThrowsAnyAsync<SocketException>(async () =>
            {
                using TcpClient client = new();
                await client.ConnectAsync("127.0.0.1", port);
            });
        }

        private static async Task<string?> SendAsync(int port, string request)
        {
            using TcpClient client = new();
            await client.ConnectAsync("127.0.0.1", port);
            NetworkStream stream = client.GetStream();
            byte[] payload = Encoding.UTF8.GetBytes(request + "\n");
            await stream.WriteAsync(payload);

            using StreamReader reader = new(stream, Encoding.UTF8);
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            return await reader.ReadLineAsync(timeout.Token);
        }

        [Fact]
        public async Task UnknownMethod_ReturnsMethodNotFoundWithRequestId()
        {
            await using RpcServer server = await StartServerAsync();

            string? response = await SendAsync(server.Port!.Value, """{"jsonrpc":"2.0","id":7,"method":"Foo","params":{}}""");

            JsonNode json = JsonNode.Parse(response!)!;
            Assert.Equal("2.0", json["jsonrpc"]!.GetValue<string>());
            Assert.Equal(7, json["id"]!.GetValue<int>());
            Assert.Equal(-32601, json["error"]!["code"]!.GetValue<int>());
        }

        [Fact]
        public async Task MalformedJson_ReturnsParseError()
        {
            await using RpcServer server = await StartServerAsync();

            string? response = await SendAsync(server.Port!.Value, "{not json");

            JsonNode json = JsonNode.Parse(response!)!;
            Assert.Null(json["id"]);
            Assert.Equal(-32700, json["error"]!["code"]!.GetValue<int>());
        }

        [Fact]
        public async Task RequestWithoutMethod_ReturnsInvalidRequest()
        {
            await using RpcServer server = await StartServerAsync();

            string? response = await SendAsync(server.Port!.Value, """{"jsonrpc":"2.0","id":1}""");

            Assert.Equal(-32600, JsonNode.Parse(response!)!["error"]!["code"]!.GetValue<int>());
        }

        [Fact]
        public async Task ServesRepeatedAndConcurrentConnections()
        {
            await using RpcServer server = await StartServerAsync();
            int port = server.Port!.Value;

            string?[] responses = await Task.WhenAll(Enumerable.Range(1, 10)
                .Select(i => SendAsync(port, $$"""{"jsonrpc":"2.0","id":{{i}},"method":"WaitForOnline"}""")));

            Assert.Equal(Enumerable.Range(1, 10), responses.Select(r => JsonNode.Parse(r!)!["id"]!.GetValue<int>()));
        }

        [Fact]
        public async Task Start_WhenPortIsInUse_ThrowsAndLeavesServerStopped()
        {
            await using RpcServer server = await StartServerAsync();
            TcpListener blocker = new(System.Net.IPAddress.Any, 0);
            blocker.Start();
            try
            {
                int busyPort = ((System.Net.IPEndPoint)blocker.LocalEndpoint).Port;

                await Assert.ThrowsAnyAsync<SocketException>(() => server.StartAsync(busyPort));

                Assert.False(server.IsRunning);
                Assert.Null(server.Port);
                await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                blocker.Stop();
            }
        }

        [Fact]
        public async Task StopAsync_ClosesOpenClientsAndReleasesPort()
        {
            RpcServer server = await StartServerAsync();
            int port = server.Port!.Value;

            using TcpClient idleClient = new();
            await idleClient.ConnectAsync("127.0.0.1", port);

            await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(server.IsRunning);
            await AssertConnectionRefusedAsync(port);
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_DoesNothing()
        {
            await using RpcServer server = new();

            await server.StopAsync();

            Assert.False(server.IsRunning);
            Assert.Null(server.Port);
        }

        [Fact]
        public async Task StartAsync_AfterStop_ServesRequestsAgain()
        {
            await using RpcServer server = await StartServerAsync();
            await server.StopAsync();

            await server.StartAsync(0);

            string? response = await SendAsync(server.Port!.Value, """{"jsonrpc":"2.0","id":1,"method":"Foo"}""");
            Assert.Equal(-32601, JsonNode.Parse(response!)!["error"]!["code"]!.GetValue<int>());
        }

        [Fact]
        public async Task StartAsync_WhenRunning_ReplacesServerOnNewPort()
        {
            await using RpcServer server = await StartServerAsync();
            int oldPort = server.Port!.Value;

            await server.StartAsync(0);

            Assert.True(server.IsRunning);
            Assert.NotEqual(oldPort, server.Port);
            using (TcpClient client = new())
                await client.ConnectAsync("127.0.0.1", server.Port!.Value);
            await AssertConnectionRefusedAsync(oldPort);
        }

        [Fact]
        public async Task StartAsync_WhenRunningOnSamePort_Restarts()
        {
            await using RpcServer server = await StartServerAsync();
            int port = server.Port!.Value;

            await server.StartAsync(port);

            Assert.Equal(port, server.Port);
            using TcpClient client = new();
            await client.ConnectAsync("127.0.0.1", port);
        }

        [Fact]
        public async Task DisposeAsync_StopsServer()
        {
            RpcServer server = await StartServerAsync();
            int port = server.Port!.Value;

            await server.DisposeAsync();

            Assert.False(server.IsRunning);
            await AssertConnectionRefusedAsync(port);
        }

        [Fact]
        public async Task StartAsync_AfterDispose_Throws()
        {
            RpcServer server = await StartServerAsync();
            await server.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => server.StartAsync(0));

            Assert.False(server.IsRunning);
            await server.DisposeAsync();
        }
    }
}
