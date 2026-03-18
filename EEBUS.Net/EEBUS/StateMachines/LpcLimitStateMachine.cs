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

        public override Task<WriteApprovalResult> ApproveActiveLimitWriteAsync(ActiveLimitWriteRequest request)
        {
            long limitValue = CalculateScaledValue(request.Value, request.Scale);
            // Rule: Limit < 0W is always rejected [LPC-001]
            if (limitValue < 0)
            {
                return Task.FromResult(WriteApprovalResult.Deny("Limit value must be positive [LPC-001]"));
            }
            return base.ApproveActiveLimitWriteAsync(request);
        }

        public Task DataUpdateFailsafeConsumptionActivePowerLimitAsync(int counter, long limit, string remoteSki)
        {
            return DataUpdateFailsafeActivePowerLimitAsync(limit);
        }
    }
}