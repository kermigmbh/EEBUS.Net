using EEBUS.Net;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.StateMachines;
using Microsoft.Extensions.Time.Testing;
using Xunit.Abstractions;

namespace TestProject1.IntegrationTests
{
    public class EffectiveLimitIntegrationTests : EebusIntegrationTests
    {
        private const long FailsafeLimit = 7200;

        public EffectiveLimitIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task WHEN_NoLimitWasSentByControlBox_THEN_GetLocalDataReturnsFailsafeLimitAndLimited()
        {
            FakeTimeProvider cemTimeProvider = new();
            using EEBUSManager cemManager = new EEBUSManager(fileSystem: FileSystem, settings: Setup.GetCEMSettings(), logger: GetLogger("CEM"), timeProvider: cemTimeProvider);
            using EEBUSManager controlBoxManager = new EEBUSManager(fileSystem: FileSystem, settings: Setup.GetControlBoxSettings(), logger: GetLogger("ControlBox"));

            await StartAndConnectManagersAsync(cemManager, controlBoxManager);

            // In state "init" the CS is limited by the failsafe limit (LPC-901 / LPP-901)
            LpcLppData? lpc = cemManager.GetLocalData().Lpc;

            Assert.NotNull(lpc);
            Assert.True(lpc.LimitActive);
            Assert.Equal(FailsafeLimit, lpc.Limit);
        }

        [Fact]
        public async Task WHEN_HeartbeatTimesOut_THEN_GetLocalDataReturnsFailsafeLimitAndLimited()
        {
            FakeTimeProvider cemTimeProvider = new();
            using EEBUSManager cemManager = new EEBUSManager(fileSystem: FileSystem, settings: Setup.GetCEMSettings(), logger: GetLogger("CEM"), timeProvider: cemTimeProvider);
            using EEBUSManager controlBoxManager = new EEBUSManager(fileSystem: FileSystem, settings: Setup.GetControlBoxSettings(), logger: GetLogger("ControlBox"));

            await StartAndConnectManagersAsync(cemManager, controlBoxManager);

            // Leave "init" via "unlimited/controlled", otherwise the timeout leads to "unlimited/autonomous" instead of failsafe
            await WriteLimitUntilAppliedAsync(controlBoxManager, cemManager, new LpcLppData() { Limit = 0, LimitActive = false }, data => data.LimitActive == false);

            Log("Simulating failsafe by advancing time by 120 seconds...");
            cemTimeProvider.Advance(LimitStateMachine.HeartbeatStateTimeout);

            LpcLppData lpc = await WaitForLpcAsync(cemManager, data => data.LimitActive == true && data.Limit == FailsafeLimit, "failsafe LPC on CEM");

            Assert.True(lpc.LimitActive);
            Assert.Equal(FailsafeLimit, lpc.Limit);
        }

        [Fact]
        public async Task WHEN_ControlBoxSendsLimit_THEN_GetLocalDataReturnsLimitAndLimited()
        {
            const long limit = 1000;
            FakeTimeProvider cemTimeProvider = new();
            using EEBUSManager cemManager = new EEBUSManager(fileSystem: FileSystem, settings: Setup.GetCEMSettings(), logger: GetLogger("CEM"), timeProvider: cemTimeProvider);
            using EEBUSManager controlBoxManager = new EEBUSManager(fileSystem: FileSystem, settings: Setup.GetControlBoxSettings(), logger: GetLogger("ControlBox"));

            await StartAndConnectManagersAsync(cemManager, controlBoxManager);

            LpcLppData lpc = await WriteLimitUntilAppliedAsync(controlBoxManager, cemManager, new LpcLppData() { Limit = limit, LimitActive = true }, data => data.LimitActive == true && data.Limit == limit);

            Assert.True(lpc.LimitActive);
            Assert.Equal(limit, lpc.Limit);
        }

        /// <summary>
        /// In state "init" the CS only accepts a write after it received a heartbeat, so the write is repeated until it has been applied.
        /// </summary>
        private async Task<LpcLppData> WriteLimitUntilAppliedAsync(EEBUSManager controlBoxManager, EEBUSManager cemManager, LpcLppData limit, Func<LpcLppData, bool> condition)
        {
            string cemSki = cemManager.GetLocalData().SKI;
            for (int attempt = 1; ; attempt++)
            {
                Log($"Sending limit from control box (attempt {attempt})...");
                await controlBoxManager.WriteDataAsync(new DeviceData() { Lpc = limit }, cemSki);
                try
                {
                    return await WaitForLpcAsync(cemManager, condition, "limit applied on CEM", timeoutMs: 3000);
                }
                catch (TimeoutException) when (attempt < 5)
                {
                }
            }
        }

        private static Task<LpcLppData> WaitForLpcAsync(EEBUSManager manager, Func<LpcLppData, bool> condition, string description, int timeoutMs = 15000)
        {
            return WaitUntilAsync(() =>
            {
                LpcLppData? lpc = manager.GetLocalData().Lpc;
                return lpc != null && condition(lpc) ? lpc : null;
            }, timeoutMs: timeoutMs, description: description);
        }
    }
}
