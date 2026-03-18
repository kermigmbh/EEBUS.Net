using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using EEBUS.Models;
using EEBUS.UseCases;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace EEBUS.StateMachines
{
    /// <summary>
    /// Limit State Machine for LPC/LPP according to EEBUS LPC Spec v1.0.0 (pages 14-17).
    /// Manages the 5 states and 13 transitions based on heartbeat and limit writes.
    /// Thread-safe implementation.
    /// </summary>
    public abstract class LimitStateMachine : IDisposable
    {
        private readonly Lock _lock = new();
        private readonly PowerDirection _direction;
        private readonly List<ILimitStateMachineEvents> _eventHandlers = new();

        private LimitState _currentState = LimitState.Init;
        private Timer? _heartbeatTimeoutTimer;
        private Timer? _limitDurationTimer;
        private Timer? _failsafeMinimumDurationTimer;
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
        private TimeSpan _failsafeMinimumDuration = TimeSpan.FromHours(2);

        // Timeout constants
        private static readonly TimeSpan HeartbeatStateTimeout = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan HeartbeatAcceptTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan InitTimeout = TimeSpan.FromSeconds(120);

        protected LimitStateMachine(PowerDirection direction, long failsafeLimit)
        {
            _direction = direction;
            _failsafeLimit = failsafeLimit;
            _initStartTime = DateTimeOffset.UtcNow;

            // Start init timeout timer (Transition 3: Init -> UnlimitedAutonomous after 120s without HB+Write)
            _initTimeoutTimer = new Timer(OnInitTimeout, null, InitTimeout, Timeout.InfiniteTimeSpan);

            Debug.WriteLine($"[LimitStateMachine:{_direction}] Initialized in Init state with failsafe limit {_failsafeLimit}W");
        }

        protected LimitStateMachine(PowerDirection direction, LocalDevice localDevice) : this(direction, localDevice.GetFailsafeLimit(direction))
        {
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
            get { lock (_lock) return _failsafeMinimumDuration; }
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
        public Task DataUpdateFailsafeActivePowerLimitAsync(long value)
        {
            Task task = Task.CompletedTask;

            lock (_lock)
            {
                var oldValue = _failsafeLimit;
                _failsafeLimit = value;

                Debug.WriteLine($"[LimitStateMachine:{_direction}] Failsafe limit updated: {oldValue}W -> {value}W");

                // If currently in a failsafe-applying state, notify of effective limit change
                // This should never happen because we must reject writes in these states though
                if (_currentState == LimitState.Init || _currentState == LimitState.Failsafe)
                {
                    task = NotifyEffectiveLimitChanged();
                }
            }
            return task;
        }

        /// <summary>
        /// Update the failsafe duration (from DeviceConfiguration write)
        /// </summary>
        public Task DataUpdateFailsafeDurationMinimumAsync(int counter, TimeSpan duration, string remoteSki)
        {
            lock (_lock)
            {
                _failsafeMinimumDuration = duration;
                Debug.WriteLine($"[LimitStateMachine:{_direction}] Failsafe duration updated to {duration}");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when a heartbeat is received from the Energy Guard.
        /// Triggers appropriate state transitions.
        /// </summary>
        public Task DataUpdateHeartbeatAsync(int counter, RemoteDevice device, uint timeout, string remoteSki)
        {
            Task task = Task.CompletedTask;

            lock (_lock)
            {
                _hasReceivedHeartbeat = true;
                _lastHeartbeatTime = DateTimeOffset.UtcNow;

                Debug.WriteLine($"[LimitStateMachine:{_direction}] Heartbeat received in state {_currentState}");

                // Handle state transitions based on current state
                string reason = "received heartbeat";
                switch (_currentState)
                {
                    case LimitState.Init:
                        task = TransitionTo(LimitState.InitPlusHeartbeat, reason);
                        break;
                    case LimitState.Failsafe:
                        task = TransitionTo(LimitState.FailsafePlusHeartbeat, reason);
                        break;
                    case LimitState.UnlimitedAutonomous:
                        task = TransitionTo(LimitState.UnlimitedAutonomousPlusHeartbeat, reason);
                        break;
                    case LimitState.InitPlusHeartbeat:
                    case LimitState.UnlimitedControlled:
                    case LimitState.Limited:
                    case LimitState.FailsafePlusHeartbeat:
                    case LimitState.UnlimitedAutonomousPlusHeartbeat:
                        // cycle in the same state to reset all necessary timers
                        task = TransitionTo(_currentState, reason);
                        break;
                }
            }

            return task;
        }

        /// <summary>
        /// Evaluate if a limit write should be auto-rejected based on state machine rules.
        /// Called BEFORE the user callback.
        /// </summary>
        public virtual async Task<WriteApprovalResult> ApproveActiveLimitWriteAsync(ActiveLimitWriteRequest request)
        {
            lock (_lock)
            {
                switch (_currentState)
                {
                    case LimitState.InitPlusHeartbeat:
                    case LimitState.Limited:
                    case LimitState.UnlimitedControlled:
                    case LimitState.FailsafePlusHeartbeat:
                    case LimitState.UnlimitedAutonomousPlusHeartbeat:
                        // allowed to accept limits here
                        break;

                    case LimitState.Init:
                    case LimitState.Failsafe:
                    case LimitState.UnlimitedAutonomous:
                        return WriteApprovalResult.Deny("Must deny limit write without heartbeat within last 60s");
                }
            }

            // accept write if no user callbacks configured
            var result = WriteApprovalResult.Accept();
            foreach (var handler in _eventHandlers.ToList())
            {
                try
                {
                    // query user handler to approve limit if all previous handlers accepted
                    result = result.Approved ? await handler.ApproveActiveLimitWriteAsync(request) : result;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LimitStateMachine:{_direction}] Error in event handler: {ex.Message}");
                }
            }

            return result;
        }

        public async Task<WriteApprovalResult> ApproveFailsafeLimitWriteAsync(FailsafeLimitWriteRequest request)
        {
            lock (_lock)
            {
                switch (_currentState)
                {
                    case LimitState.Limited:
                    case LimitState.UnlimitedControlled:
                        // allowed to accept device configuration writes here
                        break;

                    case LimitState.Init:
                    case LimitState.InitPlusHeartbeat:
                    case LimitState.Failsafe:
                    case LimitState.FailsafePlusHeartbeat:
                    case LimitState.UnlimitedAutonomous:
                    case LimitState.UnlimitedAutonomousPlusHeartbeat:
                        return WriteApprovalResult.Deny($"Must deny device configuration write in state {_currentState}");
                }
            }

            // accept write if no user callbacks configured
            var result = WriteApprovalResult.Accept();
            foreach (var handler in _eventHandlers.ToList())
            {
                try
                {
                    // query user handler to approve limit if all previous handlers accepted
                    result = result.Approved ? await handler.ApproveFailsafeLimitWriteAsync(request) : result;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LimitStateMachine:{_direction}] Error in event handler: {ex.Message}");
                }
            }

            return result;
        }

        public async Task<WriteApprovalResult> ApproveFailsafeDurationMinimumWriteAsync(FailsafeDurationWriteRequest request)
        {
            lock (_lock)
            {
                switch (_currentState)
                {
                    case LimitState.Limited:
                    case LimitState.UnlimitedControlled:
                        // allowed to accept device configuration writes here
                        break;

                    case LimitState.Init:
                    case LimitState.InitPlusHeartbeat:
                    case LimitState.Failsafe:
                    case LimitState.FailsafePlusHeartbeat:
                    case LimitState.UnlimitedAutonomous:
                    case LimitState.UnlimitedAutonomousPlusHeartbeat:
                        return WriteApprovalResult.Deny($"Must deny device configuration write in state {_currentState}");
                }
            }

            // accept write if no user callbacks configured
            var result = WriteApprovalResult.Accept();
            foreach (var handler in _eventHandlers.ToList())
            {
                try
                {
                    // query user handler to approve limit if all previous handlers accepted
                    result = result.Approved ? await handler.ApproveFailsafeMinimumDurationWriteAsync(request) : result;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LimitStateMachine:{_direction}] Error in event handler: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Called after a limit write has been approved (by both state machine and user).
        /// Triggers appropriate state transitions.
        /// </summary>
        public Task DataUpdateLimitAsync(int counter, bool active, long limit, TimeSpan duration, string remoteSki)
        {
            Task task = Task.CompletedTask;

            lock (_lock)
            {
                _hasReceivedLimitWrite = true;

                Debug.WriteLine($"[LimitStateMachine:{_direction}] Limit write accepted: active={active}, value={limit}W, state={_currentState}");

                // Calculate expiry time if duration is specified
                DateTimeOffset? expiresAt = null;
                if (duration != Timeout.InfiniteTimeSpan)
                {
                    expiresAt = DateTimeOffset.UtcNow.Add(duration);
                }

                _activeLimit = limit;
                _isLimitActive = active;
                _activeLimitExpiresAt = expiresAt;

                // Handle state transitions
                switch ((_currentState, _isLimitActive))
                {
                    case (LimitState.InitPlusHeartbeat, true):
                        // Transition 2: Init -> Limited
                        task = TransitionTo(LimitState.Limited, "Heartbeat + activated limit received");
                        break;
                    case (LimitState.InitPlusHeartbeat, false):
                        // Transition 1: Init -> UnlimitedControlled
                        task = TransitionTo(LimitState.UnlimitedControlled, "Heartbeat + deactivated limit received");
                        break;

                    case (LimitState.Limited, true):
                        // Update limit value and restart duration timer
                        task = TransitionTo(LimitState.Limited, "Limit updated");
                        break;
                    case (LimitState.Limited, false):
                        // Transition 6: Limited -> UnlimitedControlled (deactivated limit)
                        task = TransitionTo(LimitState.UnlimitedControlled, "Limit deactivated");
                        break;

                    case (LimitState.UnlimitedControlled, true):
                        // Transition 4: UnlimitedControlled -> Limited
                        task = TransitionTo(LimitState.Limited, "Limit updated");
                        break;
                    case (LimitState.UnlimitedControlled, false):
                        task = TransitionTo(LimitState.UnlimitedControlled, "Limit deactivated");
                        break;

                    case (LimitState.FailsafePlusHeartbeat, true):
                        // Transition 9: Failsafe -> Limited
                        task = TransitionTo(LimitState.Limited, "Heartbeat + activated limit received");
                        break;
                    case (LimitState.FailsafePlusHeartbeat, false):
                        // Transition 8: Failsafe -> UnlimitedControlled
                        task = TransitionTo(LimitState.UnlimitedControlled, "Heartbeat + deactivated limit received");
                        break;

                    case (LimitState.UnlimitedAutonomousPlusHeartbeat, true):
                        // Transition 12: UnlimitedAutonomous -> Limited
                        task = TransitionTo(LimitState.Limited, "Heartbeat + activated limit received");
                        break;
                    case (LimitState.UnlimitedAutonomousPlusHeartbeat, false):
                        // Transition 11: UnlimitedAutonomous -> UnlimitedControlled
                        task = TransitionTo(LimitState.UnlimitedControlled, "Heartbeat + deactivated limit received");
                        break;

                    // other states should never receive limits
                    default:
                        break;
                }
            }

            return task;
        }

        #region Timer Management

        private void ResetHeartbeatTimer(TimeSpan timeout)
        {
            _heartbeatTimeoutTimer?.Dispose();
            _heartbeatTimeoutTimer = new Timer(OnHeartbeatTimeout, null, timeout, Timeout.InfiniteTimeSpan);
        }

        private void ResetHeartbeatTimer(DateTimeOffset until)
        {
            TimeSpan timeout = until.Subtract(DateTimeOffset.UtcNow);
            if (timeout > TimeSpan.Zero)
            {
                ResetHeartbeatTimer(timeout);
            }
            else
            {
                throw new InvalidOperationException($"Illegal state transition, heartbeat expected at {until} but it is already {DateTimeOffset.UtcNow}");
            }
        }

        private void ResetHeartbeatTimer(DateTimeOffset? until)
        {
            if (until.HasValue)
            {
                ResetHeartbeatTimer(until.Value);
            }
            else
            {
                throw new InvalidOperationException($"Illegal state transition, heartbeat required for transition but not marked as received");
            }
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
                _limitDurationTimer = new Timer(OnLimitDurationExpired, null, duration.Value, Timeout.InfiniteTimeSpan);
            }
        }

        private void StopLimitDurationTimer()
        {
            _limitDurationTimer?.Dispose();
            _limitDurationTimer = null;
        }

        private void StartFailsafeMinimumDurationTimer()
        {
            StopFailsafeMinimumDurationTimer();
            _failsafeMinimumDurationTimer = new Timer(OnFailsafeDurationExpired, null, _failsafeMinimumDuration, Timeout.InfiniteTimeSpan);
        }

        private void StopFailsafeMinimumDurationTimer()
        {
            _failsafeMinimumDurationTimer?.Dispose();
            _failsafeMinimumDurationTimer = null;
        }

        private async void OnHeartbeatTimeout(object? state)
        {
            Task task = Task.CompletedTask;

            lock (_lock)
            {
                Debug.WriteLine($"[LimitStateMachine:{_direction}] Heartbeat timeout in state {_currentState}");

                switch (_currentState)
                {
                    case LimitState.InitPlusHeartbeat:
                        task = TransitionTo(LimitState.Init, "Heartbeat timeout");
                        break;

                    case LimitState.FailsafePlusHeartbeat:
                        task = TransitionTo(LimitState.Failsafe, "Heartbeat timeout");
                        break;

                    // Transitions 5 and 7: -> Failsafe
                    case LimitState.Limited:
                    case LimitState.UnlimitedControlled:
                        task = TransitionTo(LimitState.Failsafe, "Heartbeat timeout");
                        break;

                    case LimitState.UnlimitedAutonomousPlusHeartbeat:
                        task = TransitionTo(LimitState.UnlimitedAutonomous, "Heartbeat timeout");
                        break;
                }
            }

            await task;
        }

        private async void OnLimitDurationExpired(object? state)
        {
            Task task = Task.CompletedTask;

            lock (_lock)
            {
                Debug.WriteLine($"[LimitStateMachine:{_direction}] Limit duration expired in state {_currentState}");

                // Transition 6: Limited -> UnlimitedControlled (duration expired)
                if (_currentState == LimitState.Limited)
                {
                    task = TransitionTo(LimitState.UnlimitedControlled, "Limit duration expired");
                }
            }

            await task;
        }

        private async void OnFailsafeDurationExpired(object? state)
        {
            Task task = Task.CompletedTask;

            lock (_lock)
            {
                Debug.WriteLine($"[LimitStateMachine:{_direction}] Failsafe duration expired in state {_currentState}");

                // Transition 10: Failsafe -> UnlimitedAutonomous (failsafe duration expired)
                if (_currentState == LimitState.Failsafe || _currentState == LimitState.FailsafePlusHeartbeat)
                {
                    task = TransitionTo(LimitState.UnlimitedAutonomous, "Failsafe duration expired");
                }
            }
            await task;
        }

        private async void OnInitTimeout(object? state)
        {
            Task task = Task.CompletedTask;

            lock (_lock)
            {
                Debug.WriteLine($"[LimitStateMachine:{_direction}] Init timeout in state {_currentState}");

                switch (_currentState)
                {
                    // Transition 3: Init -> UnlimitedAutonomous (no HB+write within 120s)
                    case LimitState.Init:
                    case LimitState.InitPlusHeartbeat:
                        task = TransitionTo(LimitState.UnlimitedAutonomous, "No heartbeat and following write received within 120s after init");
                        break;

                    // Heartbeat received in failsafe, but no following limit within 120s [LPC-921]
                    case LimitState.Failsafe:
                    case LimitState.FailsafePlusHeartbeat:
                        task = TransitionTo(LimitState.UnlimitedAutonomous, "Heartbeat received, but no following write occurred within 120s in Failsafe state");
                        break;
                }

            }
            await task;
        }

        #endregion

        #region State Transitions

        private async Task TransitionTo(LimitState newState, string reason)
        {
            List<Task> tasks = [];

            lock (_lock)
            {
                var oldState = _currentState;
                if (oldState == newState)
                    return;

                _currentState = newState;

                Debug.WriteLine($"[LimitStateMachine:{_direction}] State transition: {oldState} -> {newState} ({reason})");

                var oldEffectiveLimit = GetEffectiveLimitForState(oldState);
                var newEffectiveLimit = GetEffectiveLimit();
                switch ((oldState, newState))
                {
                    case (LimitState.Init, LimitState.Init):
                    case (LimitState.InitPlusHeartbeat, LimitState.Init):
                        // nothing to start here, _initTimeoutTimer is already running
                        StopHeartbeatTimer();
                        break;

                    case (LimitState.Init, LimitState.InitPlusHeartbeat):
                    case (LimitState.InitPlusHeartbeat, LimitState.InitPlusHeartbeat):
                        // if we receive no heartbeats for HeartbeatTimeout, we leave this state
                        ResetHeartbeatTimer(_lastHeartbeatTime?.Add(HeartbeatAcceptTimeout));
                        break;

                    // Transition 1
                    case (LimitState.InitPlusHeartbeat, LimitState.UnlimitedControlled):
                        StopInitTimeoutTimer();
                        // if we receive no heartbeats for HeartbeatTimeout, we leave this state
                        ResetHeartbeatTimer(_lastHeartbeatTime?.Add(HeartbeatStateTimeout));
                        break;

                    // Transition 2
                    case (LimitState.InitPlusHeartbeat, LimitState.Limited):
                        StopInitTimeoutTimer();
                        // maybe start timer to leave state after duration expires
                        StartLimitDurationTimer(newEffectiveLimit.ExpiresAt - DateTimeOffset.UtcNow);
                        // if we receive no heartbeats for HeartbeatTimeout, we leave this state
                        ResetHeartbeatTimer(_lastHeartbeatTime?.Add(HeartbeatStateTimeout));
                        break;

                    // Transition 3: Init -> UnlimitedAutonomous (no HB+write within 120s)
                    case (LimitState.Init, LimitState.UnlimitedAutonomous):
                    case (LimitState.InitPlusHeartbeat, LimitState.UnlimitedAutonomous):
                        StopInitTimeoutTimer();
                        StopHeartbeatTimer();
                        break;

                    case (LimitState.UnlimitedControlled, LimitState.UnlimitedControlled):
                    case (LimitState.Limited, LimitState.Limited):
                        // if we receive no heartbeats for HeartbeatTimeout, we leave this state
                        ResetHeartbeatTimer(_lastHeartbeatTime?.Add(HeartbeatStateTimeout));
                        break;

                    // Transition 4
                    case (LimitState.UnlimitedControlled, LimitState.Limited):
                        // maybe start timer to leave state after duration expires
                        StartLimitDurationTimer(newEffectiveLimit.ExpiresAt - DateTimeOffset.UtcNow);
                        // if we receive no heartbeats for HeartbeatTimeout, we leave this state
                        ResetHeartbeatTimer(_lastHeartbeatTime?.Add(HeartbeatStateTimeout));
                        break;

                    // Transition 6: Limited -> UnlimitedControlled (duration expired)
                    case (LimitState.Limited, LimitState.UnlimitedControlled):
                        StopLimitDurationTimer();
                        // if we receive no heartbeats for HeartbeatTimeout, we leave this state
                        ResetHeartbeatTimer(_lastHeartbeatTime?.Add(HeartbeatStateTimeout));
                        break;

                    // Transitions 5 and 7: -> Failsafe
                    case (LimitState.Limited, LimitState.Failsafe):
                    case (LimitState.UnlimitedControlled, LimitState.Failsafe):
                        StopLimitDurationTimer();
                        StopHeartbeatTimer();
                        // start timer for leaving failsafe state after FailsafeMinimumDuration
                        StartFailsafeMinimumDurationTimer();
                        break;

                    case (LimitState.Failsafe, LimitState.Failsafe):
                    case (LimitState.FailsafePlusHeartbeat, LimitState.Failsafe):
                        StopHeartbeatTimer();
                        break;

                    case (LimitState.Failsafe, LimitState.FailsafePlusHeartbeat):
                        _initTimeoutTimer = new Timer(OnInitTimeout, null, InitTimeout, Timeout.InfiniteTimeSpan);
                        ResetHeartbeatTimer(_lastHeartbeatTime?.Add(HeartbeatAcceptTimeout));
                        break;

                    case (LimitState.FailsafePlusHeartbeat, LimitState.FailsafePlusHeartbeat):
                        // preparatory state for Transition 8/9
                        ResetHeartbeatTimer(_lastHeartbeatTime?.Add(HeartbeatAcceptTimeout));
                        break;

                    // Transition 8: Failsafe -> UnlimitedControlled
                    case (LimitState.FailsafePlusHeartbeat, LimitState.UnlimitedControlled):
                        StopInitTimeoutTimer();
                        StopFailsafeMinimumDurationTimer();
                        // if we receive no heartbeats for HeartbeatTimeout, we leave this state
                        ResetHeartbeatTimer(_lastHeartbeatTime?.Add(HeartbeatStateTimeout));
                        break;

                    // Transition 9: Failsafe -> Limited
                    case (LimitState.FailsafePlusHeartbeat, LimitState.Limited):
                        StopInitTimeoutTimer();
                        StopFailsafeMinimumDurationTimer();
                        // maybe start timer to leave state after duration expires
                        StartLimitDurationTimer(newEffectiveLimit.ExpiresAt - DateTimeOffset.UtcNow);
                        // if we receive no heartbeats for HeartbeatTimeout, we leave this state
                        ResetHeartbeatTimer(_lastHeartbeatTime?.Add(HeartbeatStateTimeout));
                        break;

                    // Transition 10: Failsafe -> UnlimitedAutonomous (failsafe duration expired)
                    case (LimitState.Failsafe, LimitState.UnlimitedAutonomous):
                    case (LimitState.FailsafePlusHeartbeat, LimitState.UnlimitedAutonomous):
                        StopInitTimeoutTimer();
                        StopFailsafeMinimumDurationTimer();
                        StopHeartbeatTimer();
                        break;

                    case (LimitState.UnlimitedAutonomous, LimitState.UnlimitedAutonomous):
                    case (LimitState.UnlimitedAutonomousPlusHeartbeat, LimitState.UnlimitedAutonomous):
                        StopHeartbeatTimer();
                        break;

                    case (LimitState.UnlimitedAutonomous, LimitState.UnlimitedAutonomousPlusHeartbeat):
                    case (LimitState.UnlimitedAutonomousPlusHeartbeat, LimitState.UnlimitedAutonomousPlusHeartbeat):
                        // preparatory state for Transition 11/12
                        ResetHeartbeatTimer(_lastHeartbeatTime?.Add(HeartbeatAcceptTimeout));
                        break;

                    // Transition 11: UnlimitedAutonomous -> Unlimited/controlled
                    case (LimitState.UnlimitedAutonomousPlusHeartbeat, LimitState.UnlimitedControlled):
                        // if we receive no heartbeats for HeartbeatTimeout, we leave this state
                        ResetHeartbeatTimer(_lastHeartbeatTime?.Add(HeartbeatStateTimeout));
                        break;

                    // Transition 12: UnlimitedAutonomous -> Limited
                    case (LimitState.UnlimitedAutonomousPlusHeartbeat, LimitState.Limited):
                        // maybe start timer to leave state after duration expires
                        StartLimitDurationTimer(newEffectiveLimit.ExpiresAt - DateTimeOffset.UtcNow);
                        // if we receive no heartbeats for HeartbeatTimeout, we leave this state
                        ResetHeartbeatTimer(_lastHeartbeatTime?.Add(HeartbeatStateTimeout));
                        break;

                    default:
                        throw new InvalidOperationException($"Illegal state transition: {oldState} -> {newState}");
                }
                ;

                // Notify handlers
                foreach (var handler in _eventHandlers.ToList())
                {
                    try
                    {
                        tasks.Add(handler.OnStateChanged(oldState, newState, reason));

                        // Notify if effective limit changed
                        if (oldEffectiveLimit.IsLimited != newEffectiveLimit.IsLimited ||
                            oldEffectiveLimit.Value != newEffectiveLimit.Value)
                        {
                            tasks.Add(handler.OnEffectiveLimitChanged(newEffectiveLimit));
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

            foreach (var task in tasks)
            {
                await task;
            }
        }

        /// <summary>
        /// Get the current effective limit based on state machine state
        /// </summary>
        public EffectiveLimit GetEffectiveLimit()
        {
            lock (_lock)
            {
                return GetEffectiveLimitForState(_currentState);
            }
        }

        private EffectiveLimit GetEffectiveLimitForState(LimitState state)
        {
            lock (_lock)
            {
                return state switch
                {
                    LimitState.Init => EffectiveLimit.FromFailsafe(_failsafeLimit, state),
                    LimitState.InitPlusHeartbeat => EffectiveLimit.FromFailsafe(_failsafeLimit, state),
                    LimitState.Limited => EffectiveLimit.FromActive(_activeLimit, state, _activeLimitExpiresAt),
                    LimitState.Failsafe => EffectiveLimit.FromFailsafe(_failsafeLimit, state),
                    LimitState.FailsafePlusHeartbeat => EffectiveLimit.FromFailsafe(_failsafeLimit, state),
                    LimitState.UnlimitedControlled => EffectiveLimit.Unlimited(state),
                    LimitState.UnlimitedAutonomous => EffectiveLimit.Unlimited(state),
                    LimitState.UnlimitedAutonomousPlusHeartbeat => EffectiveLimit.Unlimited(state),
                    _ => throw new InvalidOperationException($"Unknown state: {state}")
                };
            }
        }

        private async Task NotifyEffectiveLimitChanged()
        {
            var effectiveLimit = GetEffectiveLimit();
            foreach (var handler in _eventHandlers.ToList())
            {
                try
                {
                    await handler.OnEffectiveLimitChanged(effectiveLimit);
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
                    _failsafeMinimumDurationTimer?.Dispose();
                    _initTimeoutTimer?.Dispose();
                    _eventHandlers.Clear();
                }
            }

            _disposed = true;
        }

        #endregion
    }
}
