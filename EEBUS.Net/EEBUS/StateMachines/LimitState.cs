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
        /// No external control - autonomous operation.
        /// Either no Energy Guard connected or failsafe duration has expired.
        /// Effective limit: No limit (maximum power)
        /// </summary>
        UnlimitedAutonomous
    }
}
