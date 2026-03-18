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
    public class LppLimitStateMachine : LimitStateMachine, LPPEvents, LPCorLPPEvents
    {
        public LppLimitStateMachine(LocalDevice localDevice) : base(PowerDirection.Production, localDevice)
        {
        }

        public LppLimitStateMachine(long limit) : base(PowerDirection.Production, limit)
        {
        }

        public override Task<WriteApprovalResult> ApproveActiveLimitWriteAsync(ActiveLimitWriteRequest request)
        {
            // Rule: Limit > 0W is always rejected [LPP-001]
            if (request.Value > 0)
            {
                return Task.FromResult(WriteApprovalResult.Deny("Limit value must be negative [LPP-001]"));
            }
            return base.ApproveActiveLimitWriteAsync(request);
        }

        public Task DataUpdateFailsafeProductionActivePowerLimitAsync(int counter, long limit, string remoteSki)
        {
            return DataUpdateFailsafeActivePowerLimitAsync(limit);
        }
    }
}