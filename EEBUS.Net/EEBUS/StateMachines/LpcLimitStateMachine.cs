using EEBUS.Models;
using EEBUS.UseCases;
using EEBUS.UseCases.ControllableSystem;

namespace EEBUS.StateMachines
{
    /// <summary>
    /// Limit State Machine for LPC/LPP according to EEBUS LPC Spec v1.0.0 (pages 14-17).
    /// Manages the 5 states and 13 transitions based on heartbeat and limit writes.
    /// Thread-safe implementation.
    /// </summary>
    public class LpcLimitStateMachine : LimitStateMachine, LPCEvents, LPCorLPPEvents
    {
        public LpcLimitStateMachine(LocalDevice localDevice) : base(PowerDirection.Consumption, localDevice)
        {
        }

        public LpcLimitStateMachine(long limit, TimeSpan failsafeDurationMinimum) : base(PowerDirection.Consumption, limit, failsafeDurationMinimum)
        {
        }

        public LpcLimitStateMachine(long limit) : base(PowerDirection.Consumption, limit)
        {
        }

        public LpcLimitStateMachine(TimeProvider timeProvider, long limit) : base(timeProvider, PowerDirection.Consumption, limit)
        {
        }

        public override async Task<WriteApprovalResult> ApproveActiveLimitWriteAsync(ActiveLimitWriteRequest request)
        {
            // Rule: Limit < 0W is always rejected [LPC-001]
            if (request.Value < 0)
            {
                await TransitionToAsync(LimitState.UnlimitedControlled, "Denied invalid value.");
                return WriteApprovalResult.Deny("Limit value must be positive [LPC-001]");
            }
            return await base.ApproveActiveLimitWriteAsync(request);
        }

        public Task DataUpdateFailsafeConsumptionActivePowerLimitAsync(int counter, long limit, string remoteSki)
        {
            return DataUpdateFailsafeActivePowerLimitAsync(limit);
        }
    }
}