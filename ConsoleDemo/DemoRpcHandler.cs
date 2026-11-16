using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.Net.Rpc;
using EEBUS.Net.Rpc.Models;
using EEBUS.SHIP.Messages;
using EEBUS.StateMachines;

namespace ConsoleDemo
{
    /// <summary>
    /// Demo RPC implementation. Methods that are not overridden are answered with error -32601.
    /// </summary>
    internal sealed class DemoRpcHandler(EEBUSManagerRpcAdapter adapter) : RpcHandler
    {
        public override Task WaitForOnlineAsync(double timeoutSeconds, CancellationToken ct) =>
            adapter.WaitForOnlineAsync(TimeSpan.FromSeconds(timeoutSeconds), ct);

        public override Task RebootAsync(CancellationToken ct)
        {
            adapter.Reboot();
            return Task.CompletedTask;
        }

        public override Task PerformFactoryResetAsync(CancellationToken ct)
        {
            adapter.FactoryReset();
            return Task.CompletedTask;
        }

        public override Task TrustDeviceAsync(string ski, CancellationToken ct)
        {
            adapter.Manager.AddTrustedSki(ski);
            return Task.CompletedTask;
        }

        public override async Task DeleteTrustedDeviceSkiAsync(string ski, CancellationToken ct)
        {
            adapter.Manager.RemoveTrustedSki(ski);
            await adapter.Manager.DisconnectAsync(ski, ConnectionCloseReasonType.removedConnection);
        }

        public override Task<RpcLimitState> GetLpcStateAsync(CancellationToken ct)
        {
            LimitState state = adapter.Manager.GetLocalData().Lpc?.LimitState
                ?? throw new InvalidOperationException("LPC state not available.");
            return Task.FromResult(state.ToRpcLimitState());
        }

        // V360 treats any error as false, so this must not throw.
        public override Task<bool> IsHeartbeatReceivedAsync(CancellationToken ct)
        {
            try
            {
                DeviceData data = adapter.Manager.GetLocalData();
                return Task.FromResult(HasHeartbeat(data.Lpc?.LimitState) || HasHeartbeat(data.Lpp?.LimitState));
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }

        // A heartbeat timeout moves every state that has a live heartbeat to one without, so the state reflects whether it is currently arriving.
        private static bool HasHeartbeat(LimitState? state) => state is LimitState.InitPlusHeartbeat or LimitState.FailsafePlusHeartbeat
            or LimitState.UnlimitedAutonomousPlusHeartbeat or LimitState.Limited or LimitState.UnlimitedControlled;
    }
}
