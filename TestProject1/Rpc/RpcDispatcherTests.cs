using EEBUS.Net.Rpc;
using EEBUS.Net.Rpc.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TestProject1.Rpc
{
    public class RpcDispatcherTests
    {
        private const string Ski = "1451160ec72643a40f30f53f12d6c9815a7dc7aa";

        /// <summary>Implements every method: records the call and returns the configured result.</summary>
        private sealed class RecordingHandler : RpcHandler
        {
            public string? Method { get; private set; }
            public object?[]? Args { get; private set; }

            public RpcLimitState State { get; set; } = RpcLimitState.Limited;
            public RpcPowerLimit? PowerLimit { get; set; }
            public RpcMpcMeasurements Measurements { get; set; } = new();
            public RpcOhpcfProcess Process { get; set; } = new(RpcOhpcfState.Running, RpcPowerValueType.PowerMax, 3500);
            public string QrCode { get; set; } = $"SHIP;SKI:{Ski};ENDSHIP;";

            private Task Record(string method, params object?[] args)
            {
                Method = method;
                Args = args;
                return Task.CompletedTask;
            }

            private Task<T> Returns<T>(T result, string method, params object?[] args)
            {
                Record(method, args);
                return Task.FromResult(result);
            }

            public override Task WaitForOnlineAsync(double timeoutSeconds, CancellationToken ct) => Record("WaitForOnline", timeoutSeconds);
            public override Task TrustDeviceAsync(string ski, CancellationToken ct) => Record("TrustDevice", ski);
            public override Task DeleteTrustedDeviceSkiAsync(string ski, CancellationToken ct) => Record("DeleteTrustedDeviceSKI", ski);
            public override Task RebootAsync(CancellationToken ct) => Record("Reboot");
            public override Task SwitchPowerAsync(bool on, CancellationToken ct) => Record("SwitchPower", on);
            public override Task PerformFactoryResetAsync(CancellationToken ct) => Record("PerformFactoryReset");

            public override Task<RpcLimitState> GetLpcStateAsync(CancellationToken ct) => Returns(State, "GetLPCState");
            public override Task<bool> IsHeartbeatReceivedAsync(CancellationToken ct) => Returns(true, "IsHeartbeatReceived");
            public override Task<double> GetOwnActivePowerConsumptionWattAsync(CancellationToken ct) => Returns(1234.5, "GetOwnActivePowerConsumptionWatt");
            public override Task<RpcPowerLimit?> GetOwnActivePowerConsumptionLimitAsync(CancellationToken ct) => Returns(PowerLimit, "GetOwnActivePowerConsumptionLimit");

            public override Task<bool> SetPowerConsumptionLimitAsync(string remoteSki, double valueWatts, bool active, double? durationSeconds, CancellationToken ct)
                => Returns(true, "SetPowerConsumptionLimit", remoteSki, valueWatts, active, durationSeconds);
            public override Task<bool> SetFailSafeValueAsync(string remoteSki, double valueWatts, CancellationToken ct) => Returns(true, "SetFailSafeValue", remoteSki, valueWatts);
            public override Task<bool> SetFailSafeDurationAsync(string remoteSki, double durationSeconds, CancellationToken ct) => Returns(true, "SetFailSafeDuration", remoteSki, durationSeconds);

            public override Task<RpcLimitState> GetLppStateAsync(CancellationToken ct) => Returns(State, "GetLPPState");
            public override Task<double> GetOwnActivePowerProductionWattAsync(CancellationToken ct) => Returns(1234.5, "GetOwnActivePowerProductionWatt");
            public override Task<RpcPowerLimit?> GetOwnActivePowerProductionLimitAsync(CancellationToken ct) => Returns(PowerLimit, "GetOwnActivePowerProductionLimit");

            public override Task<bool> SetPowerProductionLimitAsync(string remoteSki, double valueWatts, bool active, double? durationSeconds, CancellationToken ct)
                => Returns(true, "SetPowerProductionLimit", remoteSki, valueWatts, active, durationSeconds);
            public override Task<bool> SetFailSafeProductionValueAsync(string remoteSki, double valueWatts, CancellationToken ct) => Returns(true, "SetFailSafeProductionValue", remoteSki, valueWatts);
            public override Task<bool> SetFailSafeProductionDurationAsync(string remoteSki, double durationSeconds, CancellationToken ct) => Returns(true, "SetFailSafeProductionDuration", remoteSki, durationSeconds);

            public override Task<bool> ApplyMeasurementChangeAsync(RpcMeasurand measurand, RpcPhase phase, double value, RpcValueState valueState, CancellationToken ct)
                => Returns(true, "ApplyMeasurementChange", measurand, phase, value, valueState);
            public override Task<RpcMpcMeasurements> GetMpcMeasurementsAsync(string remoteSki, CancellationToken ct) => Returns(Measurements, "GetMpcMeasurements", remoteSki);

            public override Task<RpcOhpcfProcess> GetOhpcfProcessAsync(CancellationToken ct) => Returns(Process, "GetOHPCFProcess");
            public override Task ScheduleOhpcfProcessAsync(double secondsUntilStart, CancellationToken ct) => Record("ScheduleOHPCFProcess", secondsUntilStart);
            public override Task StopOhpcfProcessAsync(CancellationToken ct) => Record("StopOHPCFProcess");
            public override Task PauseOhpcfProcessAsync(CancellationToken ct) => Record("PauseOHPCFProcess");
            public override Task ResumeOhpcfProcessAsync(CancellationToken ct) => Record("ResumeOHPCFProcess");

            public override Task<string> GetQrCodeContentAsync(CancellationToken ct) => Returns(QrCode, "GetQRCodeContent");
        }

        /// <summary>Overrides no method, so every call uses the base class's default.</summary>
        private sealed class EmptyHandler : RpcHandler { }

        // Every method, with valid params as V360 sends them, the arguments the handler receives and the JSON result.
        public static TheoryData<string, string, object?[], string> AllMethods => new()
        {
            { "WaitForOnline", """{"timeoutSeconds":300}""", [300.0], "null" },
            { "TrustDevice", $$"""{"ski":"{{Ski}}"}""", [Ski], "null" },
            { "DeleteTrustedDeviceSKI", $$"""{"ski":"{{Ski}}"}""", [Ski], "null" },
            { "Reboot", "{}", [], "null" },
            { "SwitchPower", """{"on":false}""", [false], "null" },
            { "PerformFactoryReset", "{}", [], "null" },

            { "GetLPCState", "{}", [], "\"Limited\"" },
            { "IsHeartbeatReceived", "{}", [], "true" },
            { "GetOwnActivePowerConsumptionWatt", "{}", [], "1234.5" },
            { "GetOwnActivePowerConsumptionLimit", "{}", [], "{}" },

            { "SetPowerConsumptionLimit", $$"""{"remoteSKI":"{{Ski}}","valueWatts":4200,"active":true,"durationSeconds":7200}""", [Ski, 4200.0, true, 7200.0], "true" },
            { "SetFailSafeValue", $$"""{"remoteSKI":"{{Ski}}","valueWatts":-1}""", [Ski, -1.0], "true" },
            { "SetFailSafeDuration", $$"""{"remoteSKI":"{{Ski}}","duration":7200}""", [Ski, 7200.0], "true" },

            { "GetLPPState", "{}", [], "\"Limited\"" },
            { "GetOwnActivePowerProductionWatt", "{}", [], "1234.5" },
            { "GetOwnActivePowerProductionLimit", "{}", [], "{}" },

            { "SetPowerProductionLimit", $$"""{"remoteSKI":"{{Ski}}","valueWatts":-3000.5,"active":false,"durationSeconds":null}""", [Ski, -3000.5, false, null], "true" },
            { "SetFailSafeProductionValue", $$"""{"remoteSKI":"{{Ski}}","valueWatts":5000}""", [Ski, 5000.0], "true" },
            { "SetFailSafeProductionDuration", $$"""{"remoteSKI":"{{Ski}}","duration":0}""", [Ski, 0.0], "true" },

            { "ApplyMeasurementChange", """{"measurand":"power-phase","phase":"bc","value":-1500.5,"valueState":"outOfRange"}""",
                [RpcMeasurand.PowerPhase, RpcPhase.BC, -1500.5, RpcValueState.OutOfRange], "true" },
            { "GetMpcMeasurements", $$"""{"remoteSki":"{{Ski}}"}""", [Ski], "{}" },

            { "GetOHPCFProcess", "{}", [], """{"state":"running","powerValueType":"powerMax","powerValueWatt":3500}""" },
            { "ScheduleOHPCFProcess", """{"secondsUntilStart":120}""", [120.0], "null" },
            { "StopOHPCFProcess", "{}", [], "null" },
            { "PauseOHPCFProcess", "{}", [], "null" },
            { "ResumeOHPCFProcess", "{}", [], "null" },

            { "GetQRCodeContent", "{}", [], $"\"SHIP;SKI:{Ski};ENDSHIP;\"" },
        };

        public static TheoryData<string, string> AllMethodsWithParams
        {
            get
            {
                TheoryData<string, string> data = new();
                foreach (object[] row in AllMethods)
                    data.Add((string)row[0], (string)row[1]);
                return data;
            }
        }

        private static Task<JsonNode?> InvokeAsync(RpcHandler handler, string method, string paramsJson = "{}")
            => new RpcDispatcher(handler).InvokeAsync(method, JsonNode.Parse(paramsJson), CancellationToken.None);

        private static void AssertJson(string expected, JsonNode? actual)
            => Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), actual), $"Expected {expected}, got {actual?.ToJsonString() ?? "null"}");

        [Theory]
        [MemberData(nameof(AllMethods))]
        public async Task EveryMethod_CallsHandlerWithParamsAndConvertsResult(string method, string paramsJson, object?[] expectedArgs, string expectedResult)
        {
            RecordingHandler handler = new();

            JsonNode? result = await InvokeAsync(handler, method, paramsJson);

            Assert.Equal(method, handler.Method);
            Assert.Equal(expectedArgs, handler.Args);
            AssertJson(expectedResult, result);
        }

        [Theory]
        [MemberData(nameof(AllMethodsWithParams))]
        public async Task EveryMethod_NotImplemented_ThrowsMethodNotFound(string method, string paramsJson)
        {
            RpcException ex = await Assert.ThrowsAsync<RpcException>(() => InvokeAsync(new EmptyHandler(), method, paramsJson));

            Assert.Equal(-32601, ex.Code);
            Assert.Equal($"Method not supported: {method}", ex.Message);
        }

        [Fact]
        public async Task UnknownMethod_ThrowsMethodNotFound()
        {
            RpcException ex = await Assert.ThrowsAsync<RpcException>(() => InvokeAsync(new RecordingHandler(), "getLPCState"));

            Assert.Equal(-32601, ex.Code);
            Assert.Equal("Method not found: getLPCState", ex.Message);
        }

        [Theory]
        [InlineData("GetOwnActivePowerConsumptionLimit")]
        [InlineData("GetOwnActivePowerProductionLimit")]
        public async Task PowerLimit_AllValues_AreSerialized(string method)
        {
            RecordingHandler handler = new()
            {
                PowerLimit = new RpcPowerLimit(true, -4200.5) { DurationSeconds = 3600, DurationIndefinite = false, IsLimitChangeable = true }
            };

            JsonNode? result = await InvokeAsync(handler, method);

            AssertJson("""{"active":true,"value":-4200.5,"durationSeconds":3600,"durationIndefinite":false,"isLimitChangeable":true}""", result);
        }

        [Fact]
        public async Task PowerLimit_UnsetOptionalValues_AreOmitted()
        {
            RecordingHandler handler = new() { PowerLimit = new RpcPowerLimit(false, 0) };

            JsonNode? result = await InvokeAsync(handler, "GetOwnActivePowerConsumptionLimit");

            AssertJson("""{"active":false,"value":0}""", result);
        }

        [Fact]
        public async Task GetMpcMeasurements_SerializesChannelsAndPhases()
        {
            RecordingHandler handler = new()
            {
                Measurements = new RpcMpcMeasurements
                {
                    PowerTotal = new RpcMeasurement(-1500)
                    {
                        ValueState = RpcValueState.Normal,
                        Timestamp = new DateTimeOffset(2026, 9, 8, 12, 15, 0, TimeSpan.FromHours(2))
                    },
                    PowerPerPhase = new Dictionary<RpcPhase, RpcMeasurement>
                    {
                        [RpcPhase.A] = new(-500),
                        [RpcPhase.BC] = new(-1000) { ValueState = RpcValueState.OutOfRange }
                    },
                    EnergyConsumption = new RpcMeasurement(12345.678),
                    VoltagePerPhase = new Dictionary<RpcPhase, RpcMeasurement> { [RpcPhase.AC] = new(400) },
                    Frequency = new RpcMeasurement(50.02)
                    {
                        Timestamp = new DateTimeOffset(2026, 9, 8, 10, 15, 0, 123, TimeSpan.Zero)
                    }
                }
            };

            JsonNode? result = await InvokeAsync(handler, "GetMpcMeasurements", $$"""{"remoteSki":"{{Ski}}"}""");

            AssertJson("""
                {
                    "powerTotal": { "value": -1500, "valueState": "normal", "timestamp": "2026-09-08T12:15:00+02:00" },
                    "powerPerPhase": { "a": { "value": -500 }, "bc": { "value": -1000, "valueState": "outOfRange" } },
                    "energyConsumption": { "value": 12345.678 },
                    "voltagePerPhase": { "ac": { "value": 400 } },
                    "frequency": { "value": 50.02, "timestamp": "2026-09-08T10:15:00.123+00:00" }
                }
                """, result);
        }

        [Fact]
        public async Task GetOHPCFProcess_AllValues_AreSerialized()
        {
            RecordingHandler handler = new()
            {
                Process = new RpcOhpcfProcess(RpcOhpcfState.Scheduled, RpcPowerValueType.Power, 2000)
                {
                    ScheduledStartTime = new DateTimeOffset(2026, 9, 8, 10, 15, 0, TimeSpan.Zero),
                    DurationUnknown = false,
                    IsStoppable = true,
                    IsPausable = false,
                    ActiveDurationMin = 600,
                    PauseDurationMin = 300
                }
            };

            JsonNode? result = await InvokeAsync(handler, "GetOHPCFProcess");

            AssertJson("""
                {
                    "state": "scheduled", "powerValueType": "power", "powerValueWatt": 2000, "scheduledStartTime": "2026-09-08T10:15:00+00:00",
                    "durationUnknown": false, "isStoppable": true, "isPausable": false, "activeDurationMin": 600, "pauseDurationMin": 300
                }
                """, result);
        }

        [Fact]
        public async Task GetLPPState_UndefinedState_Throws()
        {
            RecordingHandler handler = new() { State = (RpcLimitState)42 };

            await Assert.ThrowsAsync<JsonException>(() => InvokeAsync(handler, "GetLPPState"));
        }

        [Fact]
        public async Task GetOHPCFProcess_UndefinedState_Throws()
        {
            RecordingHandler handler = new() { Process = new RpcOhpcfProcess((RpcOhpcfState)42, RpcPowerValueType.Power, 0) };

            await Assert.ThrowsAsync<JsonException>(() => InvokeAsync(handler, "GetOHPCFProcess"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetQRCodeContent_Empty_Throws(string? content)
        {
            RecordingHandler handler = new() { QrCode = content! };

            await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeAsync(handler, "GetQRCodeContent"));
        }

        [Fact]
        public async Task ApplyMeasurementChange_EmptyPhase_IsNone()
        {
            RecordingHandler handler = new();

            await InvokeAsync(handler, "ApplyMeasurementChange", """{"measurand":"frequency","phase":"","value":50.1,"valueState":"normal"}""");

            Assert.Equal(new object?[] { RpcMeasurand.Frequency, RpcPhase.None, 50.1, RpcValueState.Normal }, handler.Args);
        }

        [Theory]
        [InlineData("""{"phase":"","value":1,"valueState":"normal"}""", "missing 'measurand'")]
        [InlineData("""{"measurand":"Power-Total","phase":"","value":1,"valueState":"normal"}""", "invalid value for 'measurand': \"Power-Total\"")]
        [InlineData("""{"measurand":"current","phase":"A","value":1,"valueState":"normal"}""", "invalid value for 'phase': \"A\"")]
        [InlineData("""{"measurand":"current","phase":null,"value":1,"valueState":"normal"}""", "invalid value for 'phase': null")]
        [InlineData("""{"measurand":"current","phase":"a","value":1,"valueState":0}""", "invalid value for 'valueState': 0")]
        [InlineData("""{"measurand":"current","phase":"a","value":1,"valueState":"0"}""", "invalid value for 'valueState': \"0\"")]
        [InlineData("""{"measurand":"current","phase":"a","value":"1","valueState":"normal"}""", "invalid value for 'value': \"1\"")]
        public async Task ApplyMeasurementChange_InvalidParams_ThrowsInvalidParamsWithoutCallingHandler(string paramsJson, string expectedDetail)
        {
            RecordingHandler handler = new();

            RpcException ex = await Assert.ThrowsAsync<RpcException>(() => InvokeAsync(handler, "ApplyMeasurementChange", paramsJson));

            Assert.Equal(-32602, ex.Code);
            Assert.Equal($"Invalid params for ApplyMeasurementChange: {expectedDetail}", ex.Message);
            Assert.Null(handler.Method);
        }

        [Theory]
        [InlineData("SetFailSafeDuration", """{"remoteSKI":"abc","durationSeconds":60}""", "missing 'duration'")]
        [InlineData("SetFailSafeProductionDuration", """{"remoteSKI":"abc","durationSeconds":60}""", "missing 'duration'")]
        [InlineData("GetMpcMeasurements", """{"remoteSKI":"abc"}""", "missing 'remoteSki'")]
        [InlineData("SetFailSafeValue", """{"remoteSki":"abc","valueWatts":1}""", "missing 'remoteSKI'")]
        [InlineData("DeleteTrustedDeviceSKI", """{"remoteSKI":"abc"}""", "missing 'ski'")]
        [InlineData("WaitForOnline", """{"timeout":300}""", "missing 'timeoutSeconds'")]
        [InlineData("SwitchPower", """{"on":1}""", "invalid value for 'on': 1")]
        [InlineData("SwitchPower", """{"on":"true"}""", "invalid value for 'on': \"true\"")]
        [InlineData("ScheduleOHPCFProcess", """{"secondsUntilStart":null}""", "invalid value for 'secondsUntilStart': null")]
        [InlineData("TrustDevice", """{"ski":null}""", "invalid value for 'ski': null")]
        [InlineData("TrustDevice", """{"ski":true}""", "invalid value for 'ski': true")]
        [InlineData("GetMpcMeasurements", """{"remoteSki":{"a":1}}""", "invalid value for 'remoteSki': {\"a\":1}")]
        public async Task ParameterNames_MustMatchExactly(string method, string paramsJson, string expectedDetail)
        {
            RecordingHandler handler = new();

            RpcException ex = await Assert.ThrowsAsync<RpcException>(() => InvokeAsync(handler, method, paramsJson));

            Assert.Equal(-32602, ex.Code);
            Assert.Equal($"Invalid params for {method}: {expectedDetail}", ex.Message);
            Assert.Null(handler.Method);
        }

        private static void AssertRoundTrip<T>() where T : struct, Enum
        {
            foreach (T value in Enum.GetValues<T>())
            {
                string json = JsonSerializer.Serialize(value, RpcDispatcher.JsonOptions);
                Assert.Equal(value, JsonSerializer.Deserialize<T>(json, RpcDispatcher.JsonOptions));
            }
        }

        [Fact]
        public void Enums_EveryValue_RoundTrips()
        {
            AssertRoundTrip<RpcLimitState>();
            AssertRoundTrip<RpcMeasurand>();
            AssertRoundTrip<RpcPhase>();
            AssertRoundTrip<RpcValueState>();
            AssertRoundTrip<RpcOhpcfState>();
            AssertRoundTrip<RpcPowerValueType>();
        }
    }
}
