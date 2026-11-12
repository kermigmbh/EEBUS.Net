using EEBUS.Net.Rpc;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace TestProject1.Rpc
{
    public class RpcServerTests
    {
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
            await using RpcServer server = new(0);
            server.Start();

            string? response = await SendAsync(server.Port, """{"jsonrpc":"2.0","id":7,"method":"Foo","params":{}}""");

            JsonNode json = JsonNode.Parse(response!)!;
            Assert.Equal("2.0", json["jsonrpc"]!.GetValue<string>());
            Assert.Equal(7, json["id"]!.GetValue<int>());
            Assert.Equal(-32601, json["error"]!["code"]!.GetValue<int>());
        }

        [Fact]
        public async Task MalformedJson_ReturnsParseError()
        {
            await using RpcServer server = new(0);
            server.Start();

            string? response = await SendAsync(server.Port, "{not json");

            JsonNode json = JsonNode.Parse(response!)!;
            Assert.Null(json["id"]);
            Assert.Equal(-32700, json["error"]!["code"]!.GetValue<int>());
        }

        [Fact]
        public async Task RequestWithoutMethod_ReturnsInvalidRequest()
        {
            await using RpcServer server = new(0);
            server.Start();

            string? response = await SendAsync(server.Port, """{"jsonrpc":"2.0","id":1}""");

            Assert.Equal(-32600, JsonNode.Parse(response!)!["error"]!["code"]!.GetValue<int>());
        }

        [Fact]
        public async Task ServesRepeatedAndConcurrentConnections()
        {
            await using RpcServer server = new(0);
            server.Start();

            string?[] responses = await Task.WhenAll(Enumerable.Range(1, 10)
                .Select(i => SendAsync(server.Port, $$"""{"jsonrpc":"2.0","id":{{i}},"method":"WaitForOnline"}""")));

            Assert.Equal(Enumerable.Range(1, 10), responses.Select(r => JsonNode.Parse(r!)!["id"]!.GetValue<int>()));
        }

        [Fact]
        public async Task Start_WhenPortIsInUse_ThrowsAndStopIsNoOp()
        {
            TcpListener blocker = new(System.Net.IPAddress.Any, 0);
            blocker.Start();
            try
            {
                RpcServer server = new(((System.Net.IPEndPoint)blocker.LocalEndpoint).Port);

                Assert.ThrowsAny<SocketException>(server.Start);

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
            RpcServer server = new(0);
            server.Start();
            int port = server.Port;

            using TcpClient idleClient = new();
            await idleClient.ConnectAsync("127.0.0.1", port);

            await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));

            await Assert.ThrowsAnyAsync<SocketException>(async () =>
            {
                using TcpClient client = new();
                await client.ConnectAsync("127.0.0.1", port);
            });
        }
    }
}
