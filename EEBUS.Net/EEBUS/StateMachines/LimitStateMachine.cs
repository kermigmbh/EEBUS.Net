using System.Diagnostics;
using EEBUS.UseCases;

namespace EEBUS.StateMachines
{
    /// <summary>
    /// Limit State Machine for LPC/LPP according to EEBUS LPC Spec v1.0.0 (pages 14-17).
    /// Manages the 5 states and 13 transitions based on heartbeat and limit writes.
    /// Thread-safe implementation.
    /// </summary>
    public class LimitStateMachine : IDisposable
    {
        private readonly object _lock = new();
        private readonly PowerDirection _direction;
        private readonly List<ILimitStateMachineEvents> _eventHandlers = new();

        private LimitState _currentState = LimitState.Init;
        private Timer? _heartbeatTimeoutTimer;
        private Timer? _limitDurationTimer;
        private Timer? _failsafeDurationTimer;
        private Timer? _initTimeoutTimer;

        // Limit values
        private long _failsafeLimit;
        private long _activeLimit;
        private bool _isLimitActive;
        private DateTimeOffset? _activeLimitExpiresAt;
        private bool _hasReceivedHeartbeat;
        private bool _hasReceivedLimitWrite;
        private DateTimeOffset _initStartTime;
        private DateTimeOffset? _lastHeartbeatTime;

        // Failsafe duration (2-24h per spec, default 2h)
        private TimeSpan _failsafeDuration = TimeSpan.FromHours(2);

        // Timeout constants
        private const int HeartbeatTimeoutMs = 120_000; // 120 seconds
        private const int InitTimeoutMs = 120_000; // 120 seconds for init state

        public LimitStateMachine(PowerDirection direction, long initialFailsafeLimit = 0)
        {
            _direction = direction;
            _failsafeLimit = initialFailsafeLimit;
            _initStartTime = DateTimeOffset.UtcNow;

            // Start init timeout timer (Transition 3: Init -> UnlimitedAutonomous after 120s without HB+Write)
            _initTimeoutTimer = new Timer(OnInitTimeout, null, InitTimeoutMs, Timeout.Infinite);

            Debug.WriteLine($"[LimitStateMachine:{_direction}] Initialized in Init state with failsafe limit {_failsafeLimit}W");
        }

        /// <summary>
        /// Current state of the state machine
        /// </summary>
        public LimitState CurrentState
        {
            get { lock (_lock) return _currentState; }
        }

        /// <summary>
        /// Direction this state machine manages (Consumption or Production)
        /// </summary>
        public PowerDirection Direction => _direction;

        /// <summary>
        /// Whether a heartbeat has been received from the Energy Guard
        /// </summary>
        public bool HasReceivedHeartbeat
        {
            get { lock (_lock) return _hasReceivedHeartbeat; }
        }

        /// <summary>
        /// The currently configured failsafe limit in Watts
        /// </summary>
        public long FailsafeLimit
        {
            get { lock (_lock) return _failsafeLimit; }
        }

        /// <summary>
        /// The currently configured failsafe duration
        /// </summary>
        public TimeSpan FailsafeDuration
        {
            get { lock (_lock) return _failsafeDuration; }
        }

        /// <summary>
        /// Register an event handler for state machine events
        /// </summary>
        public void RegisterEventHandler(ILimitStateMachineEvents handler)
        {
            lock (_lock)
            {
                if (!_eventHandlers.Contains(handler))
                    _eventHandlers.Add(handler);
            }
        }

        /// <summary>
        /// Unregister an event handler
        /// </summary>
        public void UnregisterEventHandler(ILimitStateMachineEvents handler)
        {
            lock (_lock)
            {
                _eventHandlers.Remove(handler);
            }
        }

        /// <summary>
        /// Update the failsafe limit value (from DeviceConfiguration write)
        /// </summary>
        public void SetFailsafeLimit(long value)
        {
            lock (_lock)
            {
                var oldValue = _failsafeLimit;
                _failsafeLimit = value;

                Debug.WriteLine($"[LimitStateMachine:{_direction}] Failsafe limit updated: {oldValue}W -> {value}W");

                // If currently in a failsafe-applying state, notify of effective limit change
                if (_currentState == LimitState.Init || _currentState == LimitState.Failsafe)
                {
                    NotifyEffectiveLimitChanged();
                }
            }
        }

        /// <summary>
        /// Update the failsafe duration (from DeviceConfiguration write)
        /// </summary>
        public void SetFailsafeDuration(TimeSpan duration)
        {
            lock (_lock)
            {
                _failsafeDuration = duration;
                Debug.WriteLine($"[LimitStateMachine:{_direction}] Failsafe duration updated to {duration}");
            }
        }

        /// <summary>
        /// Get the current effective limit based on state machine state
        /// </summary>
        public EffectiveLimit GetEffectiveLimit()
        {
            lock (_lock)
            {
                return _currentState switch
                {
                    LimitState.Init => EffectiveLimit.FromFailsafe(_failsafeLimit, _currentState),
                    LimitState.Limited => EffectiveLimit.FromActive(_activeLimit, _currentState, _activeLimitExpiresAt),
                    LimitState.Failsafe => EffectiveLimit.FromFailsafe(_failsafeLimit, _currentState),
                    LimitState.UnlimitedControlled => EffectiveLimit.Unlimited(_currentState),
                    LimitState.UnlimitedAutonomous => EffectiveLimit.Unlimited(_currentState),
                    _ => throw new InvalidOperationException($"Unknown state: {_currentState}")
                };
            }
        }

        /// <summary>
        /// Called when a heartbeat is received from the Energy Guard.
        /// Triggers appropriate state transitions.
        /// </summary>
        public void OnHeartbeatReceived()
        {
            lock (_lock)
            {
                _hasReceivedHeartbeat = true;
                _lastHeartbeatTime = DateTimeOffset.UtcNow;

                Debug.WriteLine($"[LimitStateMachine:{_direction}] Heartbeat received in state {_currentState}");

                // Reset/start heartbeat timeout timer
                ResetHeartbeatTimer();

                // Handle state transitions based on current state
                switch (_currentState)
                {
                    case LimitState.Init:
                        // Transitions 1, 2, 3 are handled when we also have limit write info
                        // If we already have a limit write, process it now
                        if (_hasReceivedLimitWrite)
                        {
                            // The limit write handler will have set _activeLimit and _activeLimitExpiresAt
                            // We need to check if the limit is active and applicable
                            ProcessInitStateWithHeartbeat();
                        }
                        // Otherwise wait for limit write or timeout
                        break;

                    case LimitState.Failsafe:
                        // Transitions 8, 9, 10 depend on limit state
                        // Stay in failsafe until a limit write comes
                        break;

                    case LimitState.UnlimitedAutonomous:
                        // Transitions 11, 12 depend on limit write
                        break;

                    case LimitState.UnlimitedControlled:
                    case LimitState.Limited:
                        // Just reset the heartbeat timer, no state change
                        break;
                }
            }
        }

        /// <summary>
        /// Evaluate if a limit write should be auto-rejected based on state machine rules.
        /// Called BEFORE the user callback.
        /// </summary>
        public LimitWriteEvaluation EvaluateLimitWrite(ActiveLimitWriteRequest request)
        {
            lock (_lock)
            {
                // Rule: Limit < 0W is always rejected [LPC-001]
                long limitValue = CalculateScaledValue(request.Value, request.Scale);
                if (limitValue < 0)
                {
                    return LimitWriteEvaluation.Reject("Limit value cannot be negative");
                }

                // In Init state without heartbeat, we still accept writes but track them
                // The actual state transition happens when heartbeat arrives or timeout

                return LimitWriteEvaluation.Allow();
            }
        }

        /// <summary>
        /// Called after a limit write has been approved (by both state machine and user).
        /// Triggers appropriate state transitions.
        /// </summary>
        public void OnLimitWriteAccepted(ActiveLimitWriteRequest request)
        {
            lock (_lock)
            {
                _hasReceivedLimitWrite = true;
                long limitValue = CalculateScaledValue(request.Value, request.Scale);
                bool isActive = request.IsLimitActive;

                Debug.WriteLine($"[LimitStateMachine:{_direction}] Limit write accepted: active={isActive}, value={limitValue}W, state={_currentState}");

                // Calculate expiry time if duration is specified
                DateTimeOffset? expiresAt = null;
                if (request.Duration.HasValue && request.Duration.Value != Timeout.InfiniteTimeSpan)
                {
                    expiresAt = DateTimeOffset.UtcNow.Add(request.Duration.Value);
                }

                _activeLimit = limitValue;
                _isLimitActive = isActive;
                _activeLimitExpiresAt = expiresAt;

                // Handle state transitions
                switch (_currentState)
                {
                    case LimitState.Init:
                        if (_hasReceivedHeartbeat)
                        {
                            ProcessInitStateWithHeartbeat();
                        }
                        // Otherwise wait for heartbeat
                        break;

                    case LimitState.UnlimitedControlled:
                        if (isActive && IsLimitApplicable(limitValue))
                        {
                            // Transition 4: UnlimitedControlled -> Limited
                            TransitionTo(LimitState.Limited, "Activated limit received");
                            StartLimitDurationTimer(request.Duration);
                        }
                        break;

                    case LimitState.Limited:
                        if (!isActive)
                        {
                            // Transition 6: Limited -> UnlimitedControlled (deactivated limit)
                            TransitionTo(LimitState.UnlimitedControlled, "Limit deactivated");
                            StopLimitDurationTimer();
                        }
                        else if (IsLimitApplicable(limitValue))
                        {
                            // Update limit value and restart duration timer
                            StartLimitDurationTimer(request.Duration);
                            NotifyEffectiveLimitChanged();
                        }
                        else
                        {
                            // Limit not applicable -> UnlimitedControlled
                            TransitionTo(LimitState.UnlimitedControlled, "Limit not applicable");
                            StopLimitDurationTimer();
                        }
                        break;

                    case LimitState.Failsafe:
                        if (_hasReceivedHeartbeat)
                        {
                            if (isActive && IsLimitApplicable(limitValue))
                            {
                                // Transition 9: Failsafe -> Limited
                                StopFailsafeDurationTimer();
                                TransitionTo(LimitState.Limited, "Heartbeat + activated applicable limit");
                                StartLimitDurationTimer(request.Duration);
                            }
                            else
                            {
                                // Transition 8: Failsafe -> UnlimitedControlled
                                StopFailsafeDurationTimer();
                                TransitionTo(LimitState.UnlimitedControlled, "Heartbeat + deactivated/non-applicable limit");
                            }
                        }
                        break;

                    case LimitState.UnlimitedAutonomous:
                        if (_hasReceivedHeartbeat)
                        {
                            if (isActive && IsLimitApplicable(limitValue))
                            {
                                // Transition 12: UnlimitedAutonomous -> Limited
                                TransitionTo(LimitState.Limited, "Heartbeat + activated applicable limit");
                                StartLimitDurationTimer(request.Duration);
                            }
                            else
                            {
                                // Transition 11: UnlimitedAutonomous -> UnlimitedControlled
                                TransitionTo(LimitState.UnlimitedControlled, "Heartbeat + deactivated/non-applicable limit");
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Process init state when we have both heartbeat and limit write info
        /// </summary>
        private void ProcessInitStateWithHeartbeat()
        {
            // Cancel init timeout since we have heartbeat
            StopInitTimeoutTimer();

            if (_hasReceivedLimitWrite)
            {
                // We have limit info - check if it's active and applicable
                // Transition 2: Init -> Limited (HB + activated limit within 120s)
                // Transition 1: Init -> UnlimitedControlled (HB + deactivated or non-applicable limit)

                if (_isLimitActive && IsLimitApplicable(_activeLimit))
                {
                    TransitionTo(LimitState.Limited, "Heartbeat + activated limit in Init");
                    if (_activeLimitExpiresAt.HasValue)
                    {
                        var remaining = _activeLimitExpiresAt.Value - DateTimeOffset.UtcNow;
                        if (remaining > TimeSpan.Zero)
                        {
                            StartLimitDurationTimer(remaining);
                        }
                    }
                }
                else
                {
                    TransitionTo(LimitState.UnlimitedControlled, "Heartbeat + deactivated/non-applicable limit in Init");
                }
            }
            else
            {
                // Heartbeat but no limit write yet - wait for it or timeout
                // Start a secondary timer if needed
            }
        }

        /// <summary>
        /// Check if a limit value is applicable (non-negative and finite)
        /// </summary>
        private bool IsLimitApplicable(long limitValue)
        {
            return limitValue >= 0 && limitValue < long.MaxValue;
        }

        /// <summary>
        /// Calculate the actual limit value from scaled number
        /// </summary>
        private long CalculateScaledValue(long number, short scale)
        {
            if (scale == 0)
                return number;
            if (scale > 0)
                return number * (long)Math.Pow(10, scale);
            return number / (long)Math.Pow(10, -scale);
        }

        #region Timer Management

        private void ResetHeartbeatTimer()
        {
            _heartbeatTimeoutTimer?.Dispose();
            _heartbeatTimeoutTimer = new Timer(OnHeartbeatTimeout, null, HeartbeatTimeoutMs, Timeout.Infinite);
        }

        private void StopHeartbeatTimer()
        {
            _heartbeatTimeoutTimer?.Dispose();
            _heartbeatTimeoutTimer = null;
        }

        private void StopInitTimeoutTimer()
        {
            _initTimeoutTimer?.Dispose();
            _initTimeoutTimer = null;
        }

        private void StartLimitDurationTimer(TimeSpan? duration)
        {
            StopLimitDurationTimer();

            if (duration.HasValue && duration.Value != Timeout.InfiniteTimeSpan && duration.Value > TimeSpan.Zero)
            {
                _activeLimitExpiresAt = DateTimeOffset.UtcNow.Add(duration.Value);
                _limitDurationTimer = new Timer(OnLimitDurationExpired, null, (int)duration.Value.TotalMilliseconds, Timeout.Infinite);
            }
            else
            {
                _activeLimitExpiresAt = null;
            }
        }

        private void StopLimitDurationTimer()
        {
            _limitDurationTimer?.Dispose();
            _limitDurationTimer = null;
            _activeLimitExpiresAt = null;
        }

        private void StartFailsafeDurationTimer()
        {
            StopFailsafeDurationTimer();
            _failsafeDurationTimer = new Timer(OnFailsafeDurationExpired, null, (int)_failsafeDuration.TotalMilliseconds, Timeout.Infinite);
        }

        private void StopFailsafeDurationTimer()
        {
            _failsafeDurationTimer?.Dispose();
            _failsafeDurationTimer = null;
        }

        private void OnHeartbeatTimeout(object? state)
        {
            lock (_lock)
            {
                Debug.WriteLine($"[LimitStateMachine:{_direction}] Heartbeat timeout in state {_currentState}");

                // Transitions 5 and 7: -> Failsafe
                if (_currentState == LimitState.UnlimitedControlled || _currentState == LimitState.Limited)
                {
                    StopLimitDurationTimer();
                    TransitionTo(LimitState.Failsafe, "Heartbeat timeout");
                    StartFailsafeDurationTimer();
                }
            }
        }

        private void OnLimitDurationExpired(object? state)
        {
            lock (_lock)
            {
                Debug.WriteLine($"[LimitStateMachine:{_direction}] Limit duration expired in state {_currentState}");

                // Transition 6: Limited -> UnlimitedControlled (duration expired)
                if (_currentState == LimitState.Limited)
                {
                    TransitionTo(LimitState.UnlimitedControlled, "Limit duration expired");
                }

                StopLimitDurationTimer();
            }
        }

        private void OnFailsafeDurationExpired(object? state)
        {
            lock (_lock)
            {
                Debug.WriteLine($"[LimitStateMachine:{_direction}] Failsafe duration expired in state {_currentState}");

                // Transition 10: Failsafe -> UnlimitedAutonomous (failsafe duration expired)
                if (_currentState == LimitState.Failsafe)
                {
                    TransitionTo(LimitState.UnlimitedAutonomous, "Failsafe duration expired");
                }

                StopFailsafeDurationTimer();
            }
        }

        private void OnInitTimeout(object? state)
        {
            lock (_lock)
            {
                Debug.WriteLine($"[LimitStateMachine:{_direction}] Init timeout in state {_currentState}");

                // Transition 3: Init -> UnlimitedAutonomous (no HB within 120s)
                if (_currentState == LimitState.Init && !_hasReceivedHeartbeat)
                {
                    TransitionTo(LimitState.UnlimitedAutonomous, "No heartbeat received within 120s after init");
                }

                StopInitTimeoutTimer();
            }
        }

        #endregion

        #region State Transitions

        private void TransitionTo(LimitState newState, string reason)
        {
            var oldState = _currentState;
            if (oldState == newState)
                return;

            _currentState = newState;

            Debug.WriteLine($"[LimitStateMachine:{_direction}] State transition: {oldState} -> {newState} ({reason})");

            // Notify handlers
            var oldEffectiveLimit = GetEffectiveLimitForState(oldState);
            var newEffectiveLimit = GetEffectiveLimit();

            foreach (var handler in _eventHandlers.ToList())
            {
                try
                {
                    handler.OnStateChanged(oldState, newState, reason);

                    // Notify if effective limit changed
                    if (oldEffectiveLimit.IsLimited != newEffectiveLimit.IsLimited ||
                        oldEffectiveLimit.Value != newEffectiveLimit.Value)
                    {
                        handler.OnEffectiveLimitChanged(newEffectiveLimit);
                    }

                    // Notify failsafe events
                    if (newState == LimitState.Failsafe && oldState != LimitState.Failsafe)
                    {
                        handler.OnFailsafeEntered(reason);
                    }
                    else if (oldState == LimitState.Failsafe && newState != LimitState.Failsafe)
                    {
                        handler.OnFailsafeExited(reason);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LimitStateMachine:{_direction}] Error in event handler: {ex.Message}");
                }
            }
        }

        private EffectiveLimit GetEffectiveLimitForState(LimitState state)
        {
            return state switch
            {
                LimitState.Init => EffectiveLimit.FromFailsafe(_failsafeLimit, state),
                LimitState.Limited => EffectiveLimit.FromActive(_activeLimit, state, _activeLimitExpiresAt),
                LimitState.Failsafe => EffectiveLimit.FromFailsafe(_failsafeLimit, state),
                LimitState.UnlimitedControlled => EffectiveLimit.Unlimited(state),
                LimitState.UnlimitedAutonomous => EffectiveLimit.Unlimited(state),
                _ => EffectiveLimit.Unlimited(state)
            };
        }

        private void NotifyEffectiveLimitChanged()
        {
            var effectiveLimit = GetEffectiveLimit();
            foreach (var handler in _eventHandlers.ToList())
            {
                try
                {
                    handler.OnEffectiveLimitChanged(effectiveLimit);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LimitStateMachine:{_direction}] Error in event handler: {ex.Message}");
                }
            }
        }

        #endregion

        #region IDisposable

        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                lock (_lock)
                {
                    _heartbeatTimeoutTimer?.Dispose();
                    _limitDurationTimer?.Dispose();
                    _failsafeDurationTimer?.Dispose();
                    _initTimeoutTimer?.Dispose();
                    _eventHandlers.Clear();
                }
            }

            _disposed = true;
        }

        #endregion
    }
}
