using EEBUS.StateMachines;

namespace EEBUS.Net.Rpc.Models;

/// <summary>
/// LPC/LPP limit state as reported over RPC. The member names are sent as-is, so they must match the protocol's strings.
/// </summary>
public enum RpcLimitState
{
    Init,
    UnlimitedControlled,
    UnlimitedAutonomous,
    Limited,
    Failsafe
}

public static class RpcLimitStateExtensions
{
    /// <summary>Maps a state machine state to its RPC state; the "PlusHeartbeat" sub-states report as their base state.</summary>
    public static RpcLimitState ToRpcLimitState(this LimitState state) => state switch
    {
        LimitState.Init or LimitState.InitPlusHeartbeat => RpcLimitState.Init,
        LimitState.UnlimitedControlled => RpcLimitState.UnlimitedControlled,
        LimitState.UnlimitedAutonomous or LimitState.UnlimitedAutonomousPlusHeartbeat => RpcLimitState.UnlimitedAutonomous,
        LimitState.Limited => RpcLimitState.Limited,
        LimitState.Failsafe or LimitState.FailsafePlusHeartbeat => RpcLimitState.Failsafe,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };
}
