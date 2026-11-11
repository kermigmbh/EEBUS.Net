using EEBUS.Net.EEBUS;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.StateMachines;
using EEBUS.UseCases;
using Microsoft.Extensions.Logging;

namespace EEBUS.Net.Handlers;

/// <summary>
/// Handles the events of the LPC limit state machine: accepts all write requests and reports changes as <see cref="DeviceData"/>.
/// </summary>
internal sealed class LpcEventHandler(EEBUSManager manager, ILogger? logger) : ILimitStateMachineEvents
{
    public Task<WriteApprovalResult> ApproveActiveLimitWriteAsync(ActiveLimitWriteRequest request)
    {
        logger?.LogDebug("LPC Active Limit Write Request: Value={Value}, Active={Active}", request.Value, request.IsLimitActive);
        return Task.FromResult(WriteApprovalResult.Accept());
    }

    public Task<WriteApprovalResult> ApproveFailsafeLimitWriteAsync(FailsafeLimitWriteRequest request)
    {
        logger?.LogDebug("LPC Failsafe Limit Write Request: Value={Value}", ScaledNumber.ToLong(request.Value, request.Scale));
        return Task.FromResult(WriteApprovalResult.Accept());
    }

    public Task<WriteApprovalResult> ApproveFailsafeDurationMinimumWriteAsync(FailsafeDurationWriteRequest request)
    {
        logger?.LogDebug("LPC Failsafe Duration Write Request: Duration={Duration}", request.Duration);
        return Task.FromResult(WriteApprovalResult.Accept());
    }

    public Task OnStateChanged(LimitState oldState, LimitState newState, string reason)
    {
        logger?.LogDebug("LPC OnStateChanged {OldState} -> {NewState} ({Reason})", oldState, newState, reason);
        return Raise(new LpcLppData { LimitState = newState });
    }

    public Task OnFailsafeEntered(string reason)
    {
        logger?.LogDebug("LPC entered Failsafe ({Reason})", reason);
        return Task.CompletedTask;
    }

    public Task OnFailsafeExited(string reason)
    {
        logger?.LogDebug("LPC left Failsafe ({Reason})", reason);
        return Task.CompletedTask;
    }

    public Task OnFailsafeLimitChanged(long oldLimit, long newLimit)
    {
        logger?.LogDebug("LPC Failsafe Limit changed {OldLimit} -> {NewLimit}", oldLimit, newLimit);
        return Raise(new LpcLppData { FailSafeLimit = newLimit });
    }

    public Task OnEffectiveLimitChanged(EffectiveLimit limit)
    {
        logger?.LogDebug("LPC effective limit changed");
        return Raise(new LpcLppData
        {
            LimitActive = limit.IsLimited,
            Limit = limit.Value,
            LimitDuration = (int?)limit.ExpiresAt?.Subtract(DateTimeOffset.Now).Duration().TotalSeconds
        });
    }

    private Task Raise(LpcLppData data) => manager.RaiseDeviceDataChangedAsync(new DeviceData { Lpc = data });
}
