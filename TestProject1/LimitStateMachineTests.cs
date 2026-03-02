using EEBUS.StateMachines;
using EEBUS.UseCases;

namespace TestProject1
{
    public class LimitStateMachineTests : IDisposable
    {
        private LimitStateMachine _stateMachine;
        private TestEventHandler _eventHandler;

        public LimitStateMachineTests()
        {
            _stateMachine = new LimitStateMachine(PowerDirection.Consumption, 1000); // 1000W failsafe
            _eventHandler = new TestEventHandler();
            _stateMachine.RegisterEventHandler(_eventHandler);
        }

        public void Dispose()
        {
            _stateMachine.Dispose();
        }

        #region Initial State Tests

        [Fact]
        public void InitialState_ShouldBeInit()
        {
            Assert.Equal(LimitState.Init, _stateMachine.CurrentState);
        }

        [Fact]
        public void InitialState_EffectiveLimit_ShouldBeFailsafe()
        {
            var limit = _stateMachine.GetEffectiveLimit();

            Assert.True(limit.IsLimited);
            Assert.Equal(1000, limit.Value);
            Assert.Equal(LimitState.Init, limit.State);
            Assert.Equal("failsafe", limit.Source);
        }

        [Fact]
        public void InitialState_HasReceivedHeartbeat_ShouldBeFalse()
        {
            Assert.False(_stateMachine.HasReceivedHeartbeat);
        }

        #endregion

        #region Heartbeat Tests

        [Fact]
        public void OnHeartbeatReceived_ShouldSetHasReceivedHeartbeat()
        {
            _stateMachine.OnHeartbeatReceived();

            Assert.True(_stateMachine.HasReceivedHeartbeat);
        }

        [Fact]
        public void Transition1_Init_To_UnlimitedControlled_WithHeartbeatAndDeactivatedLimit()
        {
            // Arrange: Create a deactivated limit request
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption,
                isActive: false,
                value: 5000,
                scale: 0,
                duration: null,
                remoteDeviceId: "test-device",
                remoteSKI: "test-ski"
            );

            // Act: Receive heartbeat, then limit write
            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request);

            // Assert
            Assert.Equal(LimitState.UnlimitedControlled, _stateMachine.CurrentState);
        }

        [Fact]
        public void Transition2_Init_To_Limited_WithHeartbeatAndActivatedLimit()
        {
            // Arrange
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption,
                isActive: true,
                value: 5000,
                scale: 0,
                duration: TimeSpan.FromMinutes(30),
                remoteDeviceId: "test-device",
                remoteSKI: "test-ski"
            );

            // Act
            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request);

            // Assert
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
            var limit = _stateMachine.GetEffectiveLimit();
            Assert.True(limit.IsLimited);
            Assert.Equal(5000, limit.Value);
            Assert.Equal("active", limit.Source);
        }

        #endregion

        #region Limited State Tests

        [Fact]
        public void Transition4_UnlimitedControlled_To_Limited_OnActivatedLimit()
        {
            // Arrange: Get to UnlimitedControlled state first
            var deactivatedRequest = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, false, 0, 0, null, "test", "test");
            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(deactivatedRequest);
            Assert.Equal(LimitState.UnlimitedControlled, _stateMachine.CurrentState);

            // Act: Send activated limit
            var activatedRequest = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 3000, 0, TimeSpan.FromMinutes(10), "test", "test");
            _stateMachine.OnLimitWriteAccepted(activatedRequest);

            // Assert
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
            Assert.Equal(3000, _stateMachine.GetEffectiveLimit().Value);
        }

        [Fact]
        public void Transition6_Limited_To_UnlimitedControlled_OnDeactivatedLimit()
        {
            // Arrange: Get to Limited state
            var activatedRequest = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 5000, 0, null, "test", "test");
            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(activatedRequest);
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);

            // Act: Deactivate limit
            var deactivatedRequest = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, false, 5000, 0, null, "test", "test");
            _stateMachine.OnLimitWriteAccepted(deactivatedRequest);

            // Assert
            Assert.Equal(LimitState.UnlimitedControlled, _stateMachine.CurrentState);
            Assert.False(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        #endregion

        #region Effective Limit Tests

        [Fact]
        public void GetEffectiveLimit_InUnlimitedControlled_ShouldReturnUnlimited()
        {
            // Arrange: Get to UnlimitedControlled state
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, false, 0, 0, null, "test", "test");
            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request);

            // Act
            var limit = _stateMachine.GetEffectiveLimit();

            // Assert
            Assert.False(limit.IsLimited);
            Assert.Equal(long.MaxValue, limit.Value);
            Assert.Equal("none", limit.Source);
        }

        [Fact]
        public void GetEffectiveLimit_InLimited_ShouldReturnActiveLimit()
        {
            // Arrange: Get to Limited state with 7500W limit
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 7500, 0, null, "test", "test");
            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request);

            // Act
            var limit = _stateMachine.GetEffectiveLimit();

            // Assert
            Assert.True(limit.IsLimited);
            Assert.Equal(7500, limit.Value);
            Assert.Equal("active", limit.Source);
            Assert.Equal(LimitState.Limited, limit.State);
        }

        [Fact]
        public void GetEffectiveLimit_WithScale_ShouldCalculateCorrectValue()
        {
            // Arrange: 5 * 10^3 = 5000W
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 5, 3, null, "test", "test");
            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request);

            // Act
            var limit = _stateMachine.GetEffectiveLimit();

            // Assert
            Assert.Equal(5000, limit.Value);
        }

        #endregion

        #region Auto-Reject Tests

        [Fact]
        public void EvaluateLimitWrite_NegativeValue_ShouldReject()
        {
            // Arrange
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, -1000, 0, null, "test", "test");

            // Act
            var result = _stateMachine.EvaluateLimitWrite(request);

            // Assert
            Assert.False(result.Allowed);
            Assert.Contains("negative", result.RejectionReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EvaluateLimitWrite_ValidValue_ShouldAllow()
        {
            // Arrange
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 5000, 0, null, "test", "test");

            // Act
            var result = _stateMachine.EvaluateLimitWrite(request);

            // Assert
            Assert.True(result.Allowed);
        }

        [Fact]
        public void EvaluateLimitWrite_ZeroValue_ShouldAllow()
        {
            // Arrange
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 0, 0, null, "test", "test");

            // Act
            var result = _stateMachine.EvaluateLimitWrite(request);

            // Assert
            Assert.True(result.Allowed);
        }

        #endregion

        #region Failsafe Configuration Tests

        [Fact]
        public void SetFailsafeLimit_ShouldUpdateEffectiveLimit_WhenInInitState()
        {
            // Act
            _stateMachine.SetFailsafeLimit(2000);

            // Assert
            var limit = _stateMachine.GetEffectiveLimit();
            Assert.Equal(2000, limit.Value);
        }

        [Fact]
        public void SetFailsafeDuration_ShouldUpdateDuration()
        {
            // Act
            _stateMachine.SetFailsafeDuration(TimeSpan.FromHours(4));

            // Assert
            Assert.Equal(TimeSpan.FromHours(4), _stateMachine.FailsafeDuration);
        }

        #endregion

        #region Event Handler Tests

        [Fact]
        public void StateChange_ShouldFireOnStateChangedEvent()
        {
            // Arrange
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 5000, 0, null, "test", "test");

            // Act
            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request);

            // Assert
            Assert.True(_eventHandler.StateChangedCalled);
            Assert.Equal(LimitState.Init, _eventHandler.LastOldState);
            Assert.Equal(LimitState.Limited, _eventHandler.LastNewState);
        }

        [Fact]
        public void StateChange_ShouldFireOnEffectiveLimitChangedEvent()
        {
            // Arrange
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 5000, 0, null, "test", "test");

            // Act
            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request);

            // Assert
            Assert.True(_eventHandler.EffectiveLimitChangedCalled);
            Assert.NotNull(_eventHandler.LastEffectiveLimit);
            Assert.Equal(5000, _eventHandler.LastEffectiveLimit!.Value);
        }

        [Fact]
        public void UnregisterEventHandler_ShouldStopReceivingEvents()
        {
            // Arrange
            _stateMachine.UnregisterEventHandler(_eventHandler);
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 5000, 0, null, "test", "test");

            // Act
            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request);

            // Assert
            Assert.False(_eventHandler.StateChangedCalled);
        }

        #endregion

        #region Direction Tests

        [Fact]
        public void Direction_ShouldMatchConstructorParameter()
        {
            Assert.Equal(PowerDirection.Consumption, _stateMachine.Direction);

            using var productionMachine = new LimitStateMachine(PowerDirection.Production, 500);
            Assert.Equal(PowerDirection.Production, productionMachine.Direction);
        }

        #endregion

        #region Limit Update in Limited State

        [Fact]
        public void LimitUpdate_InLimitedState_ShouldUpdateValue()
        {
            // Arrange: Get to Limited state with 5000W
            var request1 = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 5000, 0, null, "test", "test");
            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request1);
            Assert.Equal(5000, _stateMachine.GetEffectiveLimit().Value);

            // Act: Update to 3000W
            var request2 = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 3000, 0, null, "test", "test");
            _stateMachine.OnLimitWriteAccepted(request2);

            // Assert
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
            Assert.Equal(3000, _stateMachine.GetEffectiveLimit().Value);
        }

        #endregion

        #region Helper Class

        private class TestEventHandler : ILimitStateMachineEvents
        {
            public bool StateChangedCalled { get; private set; }
            public bool EffectiveLimitChangedCalled { get; private set; }
            public bool FailsafeEnteredCalled { get; private set; }
            public bool FailsafeExitedCalled { get; private set; }

            public LimitState? LastOldState { get; private set; }
            public LimitState? LastNewState { get; private set; }
            public EffectiveLimit? LastEffectiveLimit { get; private set; }
            public string? LastReason { get; private set; }

            public void OnStateChanged(LimitState oldState, LimitState newState, string reason)
            {
                StateChangedCalled = true;
                LastOldState = oldState;
                LastNewState = newState;
                LastReason = reason;
            }

            public void OnEffectiveLimitChanged(EffectiveLimit newLimit)
            {
                EffectiveLimitChangedCalled = true;
                LastEffectiveLimit = newLimit;
            }

            public void OnFailsafeEntered(string reason)
            {
                FailsafeEnteredCalled = true;
                LastReason = reason;
            }

            public void OnFailsafeExited(string reason)
            {
                FailsafeExitedCalled = true;
                LastReason = reason;
            }

            public void Reset()
            {
                StateChangedCalled = false;
                EffectiveLimitChangedCalled = false;
                FailsafeEnteredCalled = false;
                FailsafeExitedCalled = false;
                LastOldState = null;
                LastNewState = null;
                LastEffectiveLimit = null;
                LastReason = null;
            }
        }

        #endregion
    }
}
