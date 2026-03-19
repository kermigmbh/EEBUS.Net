using EEBUS.Models;
using EEBUS.StateMachines;
using EEBUS.UseCases;
using Microsoft.Extensions.Time.Testing;

namespace TestProject1.LimitStateMachineTests
{
    /// <summary>
    /// Tests for timer-based state transitions in LimitStateMachine.
    /// These tests use shorter timeouts for faster execution.
    /// </summary>
    public class StateTransitionTests : LpcTestRunner
    {
        public StateTransitionTests() : base()
        {
        }

        #region Real State Transitions
        [Fact]
        public async Task Transition1_Init_To_UnlimitedControlled_WithHeartbeatAndDeactivatedLimit()
        {
            // Arrange: Create a deactivated limit request
            var request = WriteRequest(
                isActive: false,
                value: 5000,
                duration: null
            );

            // Act: Receive heartbeat, then limit write
            await NotifyHeartbeat();
            Assert.Equal(LimitState.InitPlusHeartbeat, _stateMachine.CurrentState);
            await WriteLimit(request);

            // Assert
            Assert.Equal(LimitState.UnlimitedControlled, _stateMachine.CurrentState);
            Assert.Equal(2U, _eventHandler.StateChangedEventCount);
            Assert.False(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        [Fact]
        public async Task Transition2_Init_To_Limited_WithHeartbeatAndActivatedLimit()
        {
            // Arrange: Create an active limit request
            var request = WriteRequest(
                isActive: true,
                value: 4140,
                duration: null
            );

            // Act: Receive heartbeat, then limit write
            await NotifyHeartbeat();
            Assert.Equal(LimitState.InitPlusHeartbeat, _stateMachine.CurrentState);
            await WriteLimit(request);

            // Assert
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
            Assert.Equal(2U, _eventHandler.StateChangedEventCount);

            var limit = _stateMachine.GetEffectiveLimit();
            Assert.True(limit.IsLimited);
            Assert.Equal(4140, limit.Value);
            Assert.Equal("active", limit.Source);
        }

        [Fact]
        public async Task Transition3_Init_To_UnlimitedAutonomous_OnInitTimeout()
        {
            // Arrange: Start in init state
            Assert.Equal(LimitState.Init, _stateMachine.CurrentState);

            // Act: advance time
            _timeProvider.Advance(LimitStateMachine.InitTimeout.Divide(2));
            Assert.Equal(LimitState.Init, _stateMachine.CurrentState);
            _timeProvider.Advance(LimitStateMachine.InitTimeout.Divide(2).Add(TimeSpan.FromSeconds(1)));

            // Assert
            Assert.Equal(LimitState.UnlimitedAutonomous, _stateMachine.CurrentState);
            Assert.Equal(1U, _eventHandler.StateChangedEventCount);
            Assert.False(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        [Fact]
        public async Task Transition3_Init_To_UnlimitedAutonomous_OnInitTimeout_WithHeartbeats()
        {
            // Arrange: Start in init state
            Assert.Equal(LimitState.Init, _stateMachine.CurrentState);

            // Act: Advance and trigger heartbeat
            _timeProvider.Advance(LimitStateMachine.InitTimeout.Divide(2));
            await NotifyHeartbeat();
            Assert.Equal(LimitState.InitPlusHeartbeat, _stateMachine.CurrentState);

            // Advance triggering more heartbeats
            _timeProvider.Advance(LimitStateMachine.InitTimeout.Divide(4));
            await NotifyHeartbeat();
            _timeProvider.Advance(LimitStateMachine.InitTimeout.Divide(4));

            // Assert
            Assert.Equal(LimitState.UnlimitedAutonomous, _stateMachine.CurrentState);
            Assert.Equal(2U, _eventHandler.StateChangedEventCount);
            Assert.False(_stateMachine.GetEffectiveLimit().IsLimited);
        }


        [Fact]
        public async Task Transition3_Init_To_UnlimitedAutonomous_OnInitTimeout_WithLimits()
        {
            // Arrange: Start in init state
            Assert.Equal(LimitState.Init, _stateMachine.CurrentState);

            // Arrange: Create an active limit request
            var request = WriteRequest(
                isActive: true,
                value: 4140,
                duration: null
            );

            // Act: Advance and trigger write
            _timeProvider.Advance(LimitStateMachine.InitTimeout.Divide(2));
            await WriteLimit(request, shouldApprove: false);
            Assert.Equal(LimitState.Init, _stateMachine.CurrentState);

            _timeProvider.Advance(LimitStateMachine.InitTimeout.Divide(2));

            Assert.Equal(LimitState.UnlimitedAutonomous, _stateMachine.CurrentState);
            Assert.Equal(1U, _eventHandler.StateChangedEventCount);
            Assert.False(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        [Fact]
        public async Task Transition4_UnlimitedControlled_To_Limited_OnActivatedLimit()
        {
            // Arrange: Get to UnlimitedControlled state first
            var deactivatedRequest = WriteRequest(
                isActive: false,
                value: 0,
                duration: null
            );
            await NotifyHeartbeat();
            await WriteLimit(deactivatedRequest);
            Assert.Equal(LimitState.UnlimitedControlled, _stateMachine.CurrentState);

            // Act: Send activated limit
            var activatedRequest = WriteRequest(
                isActive: true,
                value: 4140,
                duration: TimeSpan.FromMinutes(10)
            );
            await WriteLimit(activatedRequest);

            // Assert
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
            Assert.Equal(4140, _stateMachine.GetEffectiveLimit().Value);
            Assert.True(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        [Fact]
        public async Task Transition5_UnlimitedControlled_To_Failsafe()
        {
            // Arrange: Get to UnlimitedControlled state first
            var deactivatedRequest = WriteRequest(
                isActive: false,
                value: 0,
                duration: null
            );
            await NotifyHeartbeat();
            await WriteLimit(deactivatedRequest);
            Assert.Equal(LimitState.UnlimitedControlled, _stateMachine.CurrentState);

            // Act: Advance time to past heartbeat timeout
            _timeProvider.Advance(LimitStateMachine.HeartbeatStateTimeout);

            // Assert
            Assert.Equal(LimitState.Failsafe, _stateMachine.CurrentState);
            Assert.Equal(DefaultFailsafeLimit, _stateMachine.GetEffectiveLimit().Value);
            Assert.Equal(1U, _eventHandler.FailsafeEnteredEventCount);
            Assert.Equal(0U, _eventHandler.FailsafeExitedEventCount);
            Assert.True(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        [Fact]
        public async Task Transition6_LimitDurationExpired_ShouldTransitionToUnlimitedControlled()
        {
            // Arrange: Get to Limited state first
            TimeSpan limitDuration = TimeSpan.FromMinutes(15);
            var request = WriteRequest(
                isActive: true,
                value: 5000,
                duration: limitDuration
            );
            await NotifyHeartbeat();
            await WriteLimit(request);
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);

            // Act: Wait for duration to expire
            await AdvanceTimeMaintainingState(limitDuration, LimitState.Limited);

            // Assert
            Assert.Equal(LimitState.UnlimitedControlled, _stateMachine.CurrentState);
            Assert.False(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        [Fact]
        public async Task Transition7_Limited_To_Failsafe()
        {
            // Arrange: Get to Limited state first
            var activatedRequest = WriteRequest(
                isActive: true,
                value: 4140,
                duration: null
            );
            await NotifyHeartbeat();
            await WriteLimit(activatedRequest);
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);

            // Act: Advance time to past heartbeat timeout
            _timeProvider.Advance(LimitStateMachine.HeartbeatStateTimeout);

            // Assert
            Assert.Equal(LimitState.Failsafe, _stateMachine.CurrentState);
            Assert.Equal(DefaultFailsafeLimit, _stateMachine.GetEffectiveLimit().Value);
            Assert.Equal(1U, _eventHandler.FailsafeEnteredEventCount);
            Assert.Equal(0U, _eventHandler.FailsafeExitedEventCount);
            Assert.True(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        [Fact]
        public async Task Transition8_Failsafe_to_UnlimitedControlled()
        {
            // Arrange: Get to Failsafe state first
            await Transition5_UnlimitedControlled_To_Failsafe();

            // Act: Send heartbeat + deactivated limit
            var deactivatedRequest = WriteRequest(
                isActive: false,
                value: 0,
                duration: null
            );
            await NotifyHeartbeat();
            await WriteLimit(deactivatedRequest);

            // Assert
            Assert.Equal(LimitState.UnlimitedControlled, _stateMachine.CurrentState);
            Assert.Equal(1U, _eventHandler.FailsafeEnteredEventCount);
            Assert.Equal(1U, _eventHandler.FailsafeExitedEventCount);
            Assert.False(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        [Fact]
        public async Task Transition9_Failsafe_to_Limited()
        {
            // Arrange: Get to Failsafe state first
            await Transition7_Limited_To_Failsafe();

            // Act: Send heartbeat + activated limit
            var activatedRequest = WriteRequest(
                isActive: true,
                value: 4140,
                duration: null
            );
            await NotifyHeartbeat();
            await WriteLimit(activatedRequest);

            // Assert
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
            Assert.Equal(1U, _eventHandler.FailsafeEnteredEventCount);
            Assert.Equal(1U, _eventHandler.FailsafeExitedEventCount);
            Assert.True(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        [Fact]
        public async Task Transition10_Failsafe_to_UnlimitedAutonomous_Timeout()
        {
            // Arrange: Get to Failsafe state first
            await Transition7_Limited_To_Failsafe();

            // Act: Advance past FailsafeMinimumDuration
            _timeProvider.Advance(_stateMachine.FailsafeDurationMinimum);

            // Assert
            Assert.Equal(LimitState.UnlimitedAutonomous, _stateMachine.CurrentState);
            Assert.Equal(1U, _eventHandler.FailsafeEnteredEventCount);
            Assert.Equal(1U, _eventHandler.FailsafeExitedEventCount);
            Assert.False(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        [Fact]
        public async Task Transition10_Failsafe_to_UnlimitedAutonomous_HeartbeatOnly()
        {
            // Arrange: Get to Failsafe state first
            await Transition7_Limited_To_Failsafe();

            // Act: Send heartbeat + wait 120s
            await NotifyHeartbeat();
            _timeProvider.Advance(TimeSpan.FromSeconds(120));

            // Assert
            Assert.Equal(LimitState.UnlimitedAutonomous, _stateMachine.CurrentState);
            Assert.Equal(1U, _eventHandler.FailsafeEnteredEventCount);
            Assert.Equal(1U, _eventHandler.FailsafeExitedEventCount);
            Assert.False(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        [Fact]
        public async Task Transition11_UnlimitedAutonomous_to_UnlimitedControlled()
        {
            // Arrange: Get to UnlimitedAutonomous state first
            await Transition10_Failsafe_to_UnlimitedAutonomous_Timeout();

            // Act: Send heartbeat + deactivated limit
            var deactivatedRequest = WriteRequest(
                isActive: false,
                value: 0,
                duration: null
            );
            await NotifyHeartbeat();
            await WriteLimit(deactivatedRequest);

            // Assert
            Assert.Equal(LimitState.UnlimitedControlled, _stateMachine.CurrentState);
            Assert.False(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        [Fact]
        public async Task Transition12_UnlimitedAutonomous_to_Limited()
        {
            // Arrange: Get to UnlimitedAutonomous state first
            await Transition10_Failsafe_to_UnlimitedAutonomous_HeartbeatOnly();

            // Act: Send heartbeat + eactivated limit
            var activatedRequest = WriteRequest(
                isActive: true,
                value: 4140,
                duration: null
            );
            await NotifyHeartbeat();
            await WriteLimit(activatedRequest);

            // Assert
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
            Assert.True(_stateMachine.GetEffectiveLimit().IsLimited);
        }
        #endregion Real State Transitions

        [Fact]
        public async Task Transition_ShouldEnterUnlimitedControlledWhenProcessingTakesLongerThanLimitDuration()
        {
            TimeSpan limitDuration = TimeSpan.FromSeconds(1);
            // Arrange: Create an active limit request
            var request = WriteRequest(
                isActive: true,
                value: 4140,
                duration: limitDuration
            );
            // Arrange: add eventhandler that takes too long
            _stateMachine.RegisterEventHandler(new SlowEventHandler(_timeProvider, limitDuration * 2));

            // Act: Receive heartbeat, then limit write
            await NotifyHeartbeat();
            Assert.Equal(LimitState.InitPlusHeartbeat, _stateMachine.CurrentState);
            await WriteLimit(request);

            // Assert: State Machine jumps through Limited immediately into UnlimitedControlled
            Assert.Equal(LimitState.UnlimitedControlled, _stateMachine.CurrentState);
            Assert.Equal(LimitState.Limited, _eventHandler.LastOldState);
            Assert.Equal(3U, _eventHandler.StateChangedEventCount);
            Assert.False(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        private class SlowEventHandler(FakeTimeProvider timeProvider, TimeSpan timeout) : ILimitStateMachineEvents
        {
            public Task<WriteApprovalResult> ApproveActiveLimitWriteAsync(ActiveLimitWriteRequest request)
            {
                timeProvider.Advance(timeout);
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public Task<WriteApprovalResult> ApproveFailsafeDurationMinimumWriteAsync(FailsafeDurationWriteRequest request)
            {
                timeProvider.Advance(timeout);
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public Task<WriteApprovalResult> ApproveFailsafeLimitWriteAsync(FailsafeLimitWriteRequest request)
            {
                timeProvider.Advance(timeout);
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public Task OnEffectiveLimitChanged(EffectiveLimit newLimit)
            {
                timeProvider.Advance(timeout);
                return Task.FromResult(WriteApprovalResult.Accept());
            }
        }
    }
}