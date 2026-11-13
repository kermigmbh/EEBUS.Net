using EEBUS.Net.Rpc;
using EEBUS.SHIP.Messages;

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
    }
}
