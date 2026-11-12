using EEBUS.Net.Rpc;
using EEBUS.Net.Rpc.Models;

namespace TestProject1.Rpc
{
    /// <summary>Test double whose behaviour is set per test; methods that are not set throw.</summary>
    internal sealed class FakeRpcHandler : RpcHandler
    {
        public Func<CancellationToken, Task<RpcLimitState>> GetLpcState { get; set; } = _ => throw new NotImplementedException();

        public Func<string, CancellationToken, Task> TrustDevice { get; set; } = (_, _) => throw new NotImplementedException();

        public Func<string, double, bool, double?, CancellationToken, Task<bool>> SetPowerConsumptionLimit { get; set; } = (_, _, _, _, _) => throw new NotImplementedException();

        public override Task<RpcLimitState> GetLpcStateAsync(CancellationToken ct) => GetLpcState(ct);

        public override Task TrustDeviceAsync(string ski, CancellationToken ct) => TrustDevice(ski, ct);

        public override Task<bool> SetPowerConsumptionLimitAsync(string remoteSki, double valueWatts, bool active, double? durationSeconds, CancellationToken ct)
            => SetPowerConsumptionLimit(remoteSki, valueWatts, active, durationSeconds, ct);
    }
}
