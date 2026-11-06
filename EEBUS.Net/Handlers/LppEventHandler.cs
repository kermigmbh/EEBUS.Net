using EEBUS.Net.EEBUS;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.StateMachines;
using EEBUS.UseCases;
using Microsoft.Extensions.Logging;

namespace EEBUS.Net.Handlers;

/// <summary>
/// Handles the events of the LPP limit state machine: accepts all write requests and reports changes as <see cref="DeviceData"/>.
/// </summary>
internal sealed class LppEventHandler(EEBUSManager manager, ILogger? logger) : ILimitStateMachineEvents
{
    public Task<WriteApprovalResult> ApproveActiveLimitWriteAsync(ActiveLimitWriteRequest request)
    {
        logger?.LogDebug("LPP Active Limit Write Request: Value={Value}, Active={Active}", request.Value, request.IsLimitActive);
        return Task.FromResult(WriteApprovalResult.Accept());
    }

    public Task<WriteApprovalResult> ApproveFailsafeLimitWriteAsync(FailsafeLimitWriteRequest request)
    {
        logger?.LogDebug("LPP Failsafe Limit Write Request: Value={Value}", ScaledNumber.ToLong(request.Value, request.Scale));
        return Task.FromResult(WriteApprovalResult.Accept());
    }

    public Task<WriteApprovalResult> ApproveFailsafeDurationMinimumWriteAsync(FailsafeDurationWriteRequest request)
    {
        logger?.LogDebug("LPP Failsafe Duration Write Request: Duration={Duration}", request.Duration);
        return Task.FromResult(WriteApprovalResult.Accept());
    }

    public Task OnStateChanged(LimitState oldState, LimitState newState, string reason)
    {
        logger?.LogDebug("LPP OnStateChanged {OldState} -> {NewState} ({Reason})", oldState, newState, reason);
        return Raise(new LpcLppData { LimitState = newState });
    }

    public Task OnFailsafeEntered(string reason)
    {
        logger?.LogDebug("LPP entered Failsafe ({Reason})", reason);
        return Task.CompletedTask;
    }

    public Task OnFailsafeExited(string reason)
    {
        logger?.LogDebug("LPP left Failsafe ({Reason})", reason);
        return Task.CompletedTask;
    }

    public Task OnFailsafeLimitChanged(long oldLimit, long newLimit)
    {
        logger?.LogDebug("LPP Failsafe Limit changed {OldLimit} -> {NewLimit}", oldLimit, newLimit);
        return Raise(new LpcLppData { FailSafeLimit = newLimit });
    }

    public Task OnEffectiveLimitChanged(EffectiveLimit limit) => Task.CompletedTask;

    private Task Raise(LpcLppData data) => manager.RaiseDeviceDataChangedAsync(new DeviceData { Lpp = data });
}
