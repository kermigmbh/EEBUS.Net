using EEBUS.Net.Rpc;
using EEBUS.Net.Rpc.Models;
using EEBUS.StateMachines;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace TestProject1.Rpc
{
    public class RpcServerHandlerTests
    {
        private static Task<JsonObject> CallAsync(RpcServer server, string method, string paramsJson = "{}")
            => SendAsync(server, $$"""{"jsonrpc":"2.0","id":1,"method":"{{method}}","params":{{paramsJson}}}""");

        private static async Task<JsonObject> SendAsync(RpcServer server, string request)
        {
            using TcpClient client = new();
            await client.ConnectAsync("127.0.0.1", server.Port!.Value);
            NetworkStream stream = client.GetStream();
            await stream.WriteAsync(Encoding.UTF8.GetBytes(request + "\n"));

            using StreamReader reader = new(stream, Encoding.UTF8);
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            string? response = await reader.ReadLineAsync(timeout.Token);
            return JsonNode.Parse(response!)!.AsObject();
        }

        private static int ErrorCode(JsonObject response) => response["error"]!["code"]!.GetValue<int>();

        private static async Task<RpcServer> StartServerAsync(Action<FakeRpcHandler> configure)
        {
            FakeRpcHandler handler = new();
            configure(handler);
            RpcServer server = new(handler);
            await server.StartAsync(0);
            return server;
        }

        [Theory]
        [InlineData(RpcLimitState.Init, "Init")]
        [InlineData(RpcLimitState.UnlimitedControlled, "UnlimitedControlled")]
        [InlineData(RpcLimitState.UnlimitedAutonomous, "UnlimitedAutonomous")]
        [InlineData(RpcLimitState.Limited, "Limited")]
        [InlineData(RpcLimitState.Failsafe, "Failsafe")]
        public async Task GetLPCState_ReturnsStateAsString(RpcLimitState state, string expected)
        {
            await using RpcServer server = await StartServerAsync(e => e.GetLpcState = _ => Task.FromResult(state));

            JsonObject response = await CallAsync(server, "GetLPCState");

            Assert.Equal(expected, response["result"]!.GetValue<string>());
        }

        [Fact]
        public async Task GetLPCState_UndefinedState_ReturnsInternalError()
        {
            await using RpcServer server = await StartServerAsync(e => e.GetLpcState = _ => Task.FromResult((RpcLimitState)42));

            JsonObject response = await CallAsync(server, "GetLPCState");

            Assert.Equal(-32000, ErrorCode(response));
        }

        [Fact]
        public async Task GetLPCState_WithoutParams_IsAccepted()
        {
            await using RpcServer server = await StartServerAsync(e => e.GetLpcState = _ => Task.FromResult(RpcLimitState.Limited));

            JsonObject response = await SendAsync(server, """{"jsonrpc":"2.0","id":1,"method":"GetLPCState"}""");

            Assert.Equal("Limited", response["result"]!.GetValue<string>());
        }

        [Fact]
        public async Task TrustDevice_PassesSkiAndReturnsNull()
        {
            string? receivedSki = null;
            await using RpcServer server = await StartServerAsync(e => e.TrustDevice = (ski, _) =>
            {
                receivedSki = ski;
                return Task.CompletedTask;
            });

            JsonObject response = await CallAsync(server, "TrustDevice", """{"ski":"1451160ec72643a40f30f53f12d6c9815a7dc7aa"}""");

            Assert.Equal("1451160ec72643a40f30f53f12d6c9815a7dc7aa", receivedSki);
            Assert.True(response.ContainsKey("result"));
            Assert.Null(response["result"]);
            Assert.False(response.ContainsKey("error"));
        }

        [Theory]
        [InlineData("{}", "missing 'ski'")]
        [InlineData("""{"ski":""}""", "invalid value for 'ski': \"\"")]
        [InlineData("""{"ski":42}""", "invalid value for 'ski': 42")]
        [InlineData("""["abc"]""", "params must be an object")]
        public async Task TrustDevice_InvalidParams_ReturnsInvalidParamsWithoutCallingHandler(string paramsJson, string expectedDetail)
        {
            bool called = false;
            await using RpcServer server = await StartServerAsync(e => e.TrustDevice = (_, _) =>
            {
                called = true;
                return Task.CompletedTask;
            });

            JsonObject response = await CallAsync(server, "TrustDevice", paramsJson);

            Assert.Equal(-32602, ErrorCode(response));
            Assert.Equal($"Invalid params for TrustDevice: {expectedDetail}", response["error"]!["message"]!.GetValue<string>());
            Assert.False(called);
        }

        [Fact]
        public async Task TrustDevice_HandlerThrows_ReturnsInternalErrorWithMessage()
        {
            await using RpcServer server = await StartServerAsync(e => e.TrustDevice = (_, _) => throw new InvalidOperationException("trust store full"));

            JsonObject response = await CallAsync(server, "TrustDevice", """{"ski":"abc"}""");

            Assert.Equal(-32000, ErrorCode(response));
            Assert.Equal("trust store full", response["error"]!["message"]!.GetValue<string>());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SetPowerConsumptionLimit_PassesParamsUnchangedAndReturnsResult(bool accepted)
        {
            (string Ski, double Watts, bool Active, double? Duration)? received = null;
            await using RpcServer server = await StartServerAsync(e => e.SetPowerConsumptionLimit = (ski, watts, active, duration, _) =>
            {
                received = (ski, watts, active, duration);
                return Task.FromResult(accepted);
            });

            JsonObject response = await CallAsync(server, "SetPowerConsumptionLimit",
                """{"active":true,"durationSeconds":7200,"remoteSKI":"1451160ec72643a40f30f53f12d6c9815a7dc7aa","valueWatts":4200.5}""");

            Assert.Equal(("1451160ec72643a40f30f53f12d6c9815a7dc7aa", 4200.5, true, (double?)7200), received);
            Assert.Equal(accepted, response["result"]!.GetValue<bool>());
        }

        [Theory]
        [InlineData("""{"remoteSKI":"abc","valueWatts":-1,"active":false,"durationSeconds":null}""")]
        [InlineData("""{"remoteSKI":"abc","valueWatts":-1,"active":false}""")]
        public async Task SetPowerConsumptionLimit_NullOrMissingDuration_PassesNull(string paramsJson)
        {
            double? receivedDuration = 0;
            double receivedWatts = 0;
            await using RpcServer server = await StartServerAsync(e => e.SetPowerConsumptionLimit = (_, watts, _, duration, _) =>
            {
                receivedWatts = watts;
                receivedDuration = duration;
                return Task.FromResult(true);
            });

            await CallAsync(server, "SetPowerConsumptionLimit", paramsJson);

            Assert.Null(receivedDuration);
            Assert.Equal(-1, receivedWatts);
        }

        [Theory]
        [InlineData("""{"valueWatts":1,"active":true}""", "missing 'remoteSKI'")]
        [InlineData("""{"remoteSKI":"abc","active":true}""", "missing 'valueWatts'")]
        [InlineData("""{"remoteSKI":"abc","valueWatts":"1","active":true}""", "invalid value for 'valueWatts': \"1\"")]
        [InlineData("""{"remoteSKI":"abc","valueWatts":1}""", "missing 'active'")]
        [InlineData("""{"remoteSKI":"abc","valueWatts":1,"active":"true"}""", "invalid value for 'active': \"true\"")]
        [InlineData("""{"remoteSKI":"abc","valueWatts":1,"active":true,"durationSeconds":"60"}""", "invalid value for 'durationSeconds': \"60\"")]
        public async Task SetPowerConsumptionLimit_InvalidParams_ReturnsInvalidParams(string paramsJson, string expectedDetail)
        {
            bool called = false;
            await using RpcServer server = await StartServerAsync(e => e.SetPowerConsumptionLimit = (_, _, _, _, _) =>
            {
                called = true;
                return Task.FromResult(true);
            });

            JsonObject response = await CallAsync(server, "SetPowerConsumptionLimit", paramsJson);

            Assert.Equal(-32602, ErrorCode(response));
            Assert.Equal($"Invalid params for SetPowerConsumptionLimit: {expectedDetail}", response["error"]!["message"]!.GetValue<string>());
            Assert.False(called);
        }

        private sealed class OfflineHandler : IRpcHandler
        {
            public Task WaitForOnlineAsync(double timeoutSeconds, CancellationToken ct)
                => throw new TimeoutException($"Device not online after {timeoutSeconds} s.");
        }

        [Fact]
        public async Task WaitForOnline_Timeout_ReturnsTimeoutError()
        {
            await using RpcServer server = new(new OfflineHandler());
            await server.StartAsync(0);

            JsonObject response = await CallAsync(server, "WaitForOnline", """{"timeoutSeconds":5}""");

            Assert.Equal(-32001, ErrorCode(response));
            Assert.Equal("Device not online after 5 s.", response["error"]!["message"]!.GetValue<string>());
        }

        [Fact]
        public async Task NotImplementedMethod_ReturnsMethodNotFound()
        {
            await using RpcServer server = new(new OfflineHandler());
            await server.StartAsync(0);

            JsonObject response = await CallAsync(server, "GetOHPCFProcess");

            Assert.Equal(-32601, ErrorCode(response));
            Assert.Equal("Method not supported: GetOHPCFProcess", response["error"]!["message"]!.GetValue<string>());
        }

        [Theory]
        [InlineData(LimitState.Init, RpcLimitState.Init)]
        [InlineData(LimitState.InitPlusHeartbeat, RpcLimitState.Init)]
        [InlineData(LimitState.UnlimitedControlled, RpcLimitState.UnlimitedControlled)]
        [InlineData(LimitState.UnlimitedAutonomous, RpcLimitState.UnlimitedAutonomous)]
        [InlineData(LimitState.UnlimitedAutonomousPlusHeartbeat, RpcLimitState.UnlimitedAutonomous)]
        [InlineData(LimitState.Limited, RpcLimitState.Limited)]
        [InlineData(LimitState.Failsafe, RpcLimitState.Failsafe)]
        [InlineData(LimitState.FailsafePlusHeartbeat, RpcLimitState.Failsafe)]
        public void ToRpcLimitState_MapsEveryLimitState(LimitState state, RpcLimitState expected)
        {
            Assert.Equal(expected, state.ToRpcLimitState());
        }
    }
}
