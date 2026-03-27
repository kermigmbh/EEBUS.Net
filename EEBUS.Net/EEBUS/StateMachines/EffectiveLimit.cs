namespace EEBUS.StateMachines
{
    /// <summary>
    /// Represents the current effective power limit based on the state machine state.
    /// This is the value that should be applied by the Controllable System.
    /// </summary>
    public class EffectiveLimit(bool isLimited, long value, LimitState state, string source, DateTimeOffset? expiresAt = null)
    {
        /// <summary>
        /// Whether a power limit is currently in effect
        /// </summary>
        public bool IsLimited { get; } = isLimited;

        /// <summary>
        /// The limit value in Watts. When IsLimited is false, this is long.MaxValue (no limit).
        /// </summary>
        public long Value { get; } = value;

        /// <summary>
        /// The current state of the state machine
        /// </summary>
        public LimitState State { get; } = state;

        /// <summary>
        /// Source of the limit: "active" (from EG write), "failsafe" (failsafe limit), or "none" (unlimited)
        /// </summary>
        public string Source { get; } = source;

        /// <summary>
        /// When the current limit expires (for Limited state with duration), null if no expiry
        /// </summary>
        public DateTimeOffset? ExpiresAt { get; } = expiresAt;

        /// <summary>
        /// Creates an unlimited effective limit
        /// </summary>
        public static EffectiveLimit Unlimited(LimitState state) =>
            new(false, long.MaxValue, state, "none");

        /// <summary>
        /// Creates a failsafe effective limit
        /// </summary>
        public static EffectiveLimit FromFailsafe(long failsafeValue, LimitState state) =>
            new(true, failsafeValue, state, "failsafe");

        /// <summary>
        /// Creates an active effective limit
        /// </summary>
        public static EffectiveLimit FromActive(long activeValue, LimitState state, DateTimeOffset? expiresAt = null) =>
            new(true, activeValue, state, "active", expiresAt);

        public override string ToString()
        {
            if (IsLimited)
                return $"EffectiveLimit[{State}]: {Value}W from {Source}" + (ExpiresAt.HasValue ? $" (expires {ExpiresAt})" : "");
            return $"EffectiveLimit[{State}]: Unlimited";
        }
    }
}
