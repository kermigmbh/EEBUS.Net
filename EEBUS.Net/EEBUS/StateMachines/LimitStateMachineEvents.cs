namespace EEBUS.StateMachines
{
    /// <summary>
    /// Events fired by the LimitStateMachine for state changes and limit updates.
    /// Implement this interface to receive notifications about state machine transitions.
    /// </summary>
    public interface ILimitStateMachineEvents
    {
        /// <summary>
        /// Called when the state machine transitions to a new state
        /// </summary>
        /// <param name="oldState">The previous state</param>
        /// <param name="newState">The new state</param>
        /// <param name="reason">Human-readable reason for the transition</param>
        void OnStateChanged(LimitState oldState, LimitState newState, string reason);

        /// <summary>
        /// Called when the effective limit changes (either due to state change or new limit value)
        /// </summary>
        /// <param name="newLimit">The new effective limit</param>
        void OnEffectiveLimitChanged(EffectiveLimit newLimit);

        /// <summary>
        /// Called when entering the Failsafe state
        /// </summary>
        /// <param name="reason">Reason for entering failsafe (e.g., "Heartbeat timeout")</param>
        void OnFailsafeEntered(string reason);

        /// <summary>
        /// Called when exiting the Failsafe state
        /// </summary>
        /// <param name="reason">Reason for exiting failsafe (e.g., "Heartbeat received with active limit")</param>
        void OnFailsafeExited(string reason);
    }

    /// <summary>
    /// Result of evaluating whether a limit write should be auto-rejected by the state machine
    /// </summary>
    public class LimitWriteEvaluation
    {
        /// <summary>
        /// Whether the write is allowed to proceed to user callback
        /// </summary>
        public bool Allowed { get; }

        /// <summary>
        /// If not allowed, the reason for rejection
        /// </summary>
        public string? RejectionReason { get; }

        /// <summary>
        /// If not allowed, the SPINE error code (7 = denied)
        /// </summary>
        public int ErrorCode { get; }

        private LimitWriteEvaluation(bool allowed, string? rejectionReason, int errorCode)
        {
            Allowed = allowed;
            RejectionReason = rejectionReason;
            ErrorCode = errorCode;
        }

        public static LimitWriteEvaluation Allow() => new(true, null, 0);

        public static LimitWriteEvaluation Reject(string reason, int errorCode = 7) =>
            new(false, reason, errorCode);
    }
}
