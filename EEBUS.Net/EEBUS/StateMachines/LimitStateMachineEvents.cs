using EEBUS.UseCases;

namespace EEBUS.StateMachines
{
    /// <summary>
    /// Events fired by the LimitStateMachine for state changes and limit updates.
    /// Implement this interface to receive notifications about state machine transitions.
    /// </summary>
    public interface ILimitStateMachineEvents
    {
		// Write Approval methods (called BEFORE write is applied)
		Task<WriteApprovalResult> ApproveActiveLimitWriteAsync( ActiveLimitWriteRequest request );

		Task<WriteApprovalResult> ApproveFailsafeLimitWriteAsync( FailsafeLimitWriteRequest request );

		Task<WriteApprovalResult> ApproveFailsafeDurationMinimumWriteAsync( FailsafeDurationWriteRequest request );

        /// <summary>
        /// Called when the state machine transitions to a new state
        /// </summary>
        /// <param name="oldState">The previous state</param>
        /// <param name="newState">The new state</param>
        /// <param name="reason">Human-readable reason for the transition</param>
        Task OnStateChanged(LimitState oldState, LimitState newState, string reason)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the effective limit changes (either due to state change or new limit value)
        /// </summary>
        /// <param name="newLimit">The new effective limit</param>
        Task OnEffectiveLimitChanged(EffectiveLimit newLimit);

        /// <summary>
        /// Called when entering the Failsafe state
        /// </summary>
        /// <param name="reason">Reason for entering failsafe (e.g., "Heartbeat timeout")</param>
        Task OnFailsafeEntered(string reason)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when exiting the Failsafe state
        /// </summary>
        /// <param name="reason">Reason for exiting failsafe (e.g., "Heartbeat received with active limit")</param>
        Task OnFailsafeExited(string reason)
        {
            return Task.CompletedTask;
        }
    }
}
