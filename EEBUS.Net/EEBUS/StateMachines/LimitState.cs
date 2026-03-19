namespace EEBUS.StateMachines
{
    /// <summary>
    /// States for the LPC/LPP Limit State Machine according to EEBUS LPC Spec v1.0.0 (pages 14-17)
    /// </summary>
    public enum LimitState
    {
        /// <summary>
        /// Initial state after (re-)start of the Controllable System.
        /// Effective limit: Failsafe Limit
        /// </summary>
        Init,

        /// <summary>
        /// State after receiving a heartbeat in Init state.
        /// Effective limit: Failsafe Limit
        /// </summary>
        InitPlusHeartbeat,

        /// <summary>
        /// Not limited, but controlled by the Energy Guard.
        /// Heartbeat is active and no active limit is set.
        /// Effective limit: No limit (maximum power)
        /// </summary>
        UnlimitedControlled,

        /// <summary>
        /// Actively limited by the Energy Guard.
        /// An active power limit is in effect.
        /// Effective limit: Active Power Limit
        /// </summary>
        Limited,

        /// <summary>
        /// Not controlled state due to heartbeat timeout.
        /// The Energy Guard has not sent a heartbeat within the required timeframe.
        /// Effective limit: Failsafe Limit
        /// </summary>
        Failsafe,

        /// <summary>
        /// Not controlled state due to heartbeat timeout.
        /// State is reached from Failsafe when a heartbeat is received
        /// Effective limit: Failsafe Limit
        /// </summary>
        FailsafePlusHeartbeat,

        /// <summary>
        /// No external control - autonomous operation.
        /// Either no Energy Guard connected or failsafe duration has expired.
        /// Effective limit: No limit (maximum power)
        /// </summary>
        UnlimitedAutonomous,

        /// <summary>
        /// No external control - autonomous operation.
        /// State is reached from UnlimitedAutonomous when a heartbeat is received
        /// Effective limit: No limit (maximum power)
        /// </summary>
        UnlimitedAutonomousPlusHeartbeat
    }

    public static class Extensions
    {
        private static readonly List<LimitState> failsafeStates = [LimitState.Failsafe, LimitState.FailsafePlusHeartbeat];

        public static bool IsFailsafe(this LimitState state) => failsafeStates.Contains(state);
    }
}
