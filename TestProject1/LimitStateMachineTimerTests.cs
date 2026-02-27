using EEBUS.StateMachines;
using EEBUS.UseCases;

namespace TestProject1
{
    /// <summary>
    /// Tests for timer-based state transitions in LimitStateMachine.
    /// These tests use shorter timeouts for faster execution.
    /// </summary>
    public class LimitStateMachineTimerTests : IDisposable
    {
        private LimitStateMachine? _stateMachine;
        private TestTimerEventHandler _eventHandler;

        public LimitStateMachineTimerTests()
        {
            _eventHandler = new TestTimerEventHandler();
        }

        public void Dispose()
        {
            _stateMachine?.Dispose();
        }

        [Fact]
        public async Task Transition6_LimitDurationExpired_ShouldTransitionToUnlimitedControlled()
        {
            // Arrange: Create state machine and get to Limited state with short duration
            _stateMachine = new LimitStateMachine(PowerDirection.Consumption, 1000);
            _stateMachine.RegisterEventHandler(_eventHandler);

            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption,
                isActive: true,
                value: 5000,
                scale: 0,
                duration: TimeSpan.FromMilliseconds(100), // Very short duration for testing
                remoteDeviceId: "test",
                remoteSKI: "test"
            );

            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request);
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);

            // Act: Wait for duration to expire
            await Task.Delay(200);

            // Assert
            Assert.Equal(LimitState.UnlimitedControlled, _stateMachine.CurrentState);
            Assert.False(_stateMachine.GetEffectiveLimit().IsLimited);
        }

        [Fact]
        public async Task LimitWithNoDuration_ShouldNotExpire()
        {
            // Arrange
            _stateMachine = new LimitStateMachine(PowerDirection.Consumption, 1000);

            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption,
                isActive: true,
                value: 5000,
                scale: 0,
                duration: null, // No duration
                remoteDeviceId: "test",
                remoteSKI: "test"
            );

            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request);

            // Act: Wait some time
            await Task.Delay(100);

            // Assert: Should still be Limited
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
            Assert.True(_stateMachine.GetEffectiveLimit().IsLimited);
            Assert.Equal(5000, _stateMachine.GetEffectiveLimit().Value);
        }

        [Fact]
        public async Task LimitWithInfiniteDuration_ShouldNotExpire()
        {
            // Arrange
            _stateMachine = new LimitStateMachine(PowerDirection.Consumption, 1000);

            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption,
                isActive: true,
                value: 5000,
                scale: 0,
                duration: Timeout.InfiniteTimeSpan,
                remoteDeviceId: "test",
                remoteSKI: "test"
            );

            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request);

            // Act: Wait some time
            await Task.Delay(100);

            // Assert: Should still be Limited
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
        }

        [Fact]
        public void EffectiveLimit_ExpiresAt_ShouldBeSetWhenDurationProvided()
        {
            // Arrange
            _stateMachine = new LimitStateMachine(PowerDirection.Consumption, 1000);
            var duration = TimeSpan.FromHours(1);

            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption,
                isActive: true,
                value: 5000,
                scale: 0,
                duration: duration,
                remoteDeviceId: "test",
                remoteSKI: "test"
            );

            // Act
            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request);

            // Assert
            var limit = _stateMachine.GetEffectiveLimit();
            Assert.NotNull(limit.ExpiresAt);
            Assert.True(limit.ExpiresAt > DateTimeOffset.UtcNow);
            Assert.True(limit.ExpiresAt < DateTimeOffset.UtcNow.AddHours(2));
        }

        [Fact]
        public void EffectiveLimit_ExpiresAt_ShouldBeNullWhenNoDuration()
        {
            // Arrange
            _stateMachine = new LimitStateMachine(PowerDirection.Consumption, 1000);

            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption,
                isActive: true,
                value: 5000,
                scale: 0,
                duration: null,
                remoteDeviceId: "test",
                remoteSKI: "test"
            );

            // Act
            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request);

            // Assert
            var limit = _stateMachine.GetEffectiveLimit();
            Assert.Null(limit.ExpiresAt);
        }

        [Fact]
        public async Task NewLimitWrite_ShouldResetDurationTimer()
        {
            // Arrange
            _stateMachine = new LimitStateMachine(PowerDirection.Consumption, 1000);

            var request1 = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 5000, 0,
                TimeSpan.FromMilliseconds(100), "test", "test");

            _stateMachine.OnHeartbeatReceived();
            _stateMachine.OnLimitWriteAccepted(request1);

            // Act: After 50ms, send new limit with new duration
            await Task.Delay(50);
            var request2 = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 3000, 0,
                TimeSpan.FromMilliseconds(200), "test", "test");
            _stateMachine.OnLimitWriteAccepted(request2);

            // Wait past original timeout but before new timeout
            await Task.Delay(100);

            // Assert: Should still be Limited (timer was reset)
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
            Assert.Equal(3000, _stateMachine.GetEffectiveLimit().Value);

            // Wait for new duration to expire
            await Task.Delay(150);
            Assert.Equal(LimitState.UnlimitedControlled, _stateMachine.CurrentState);
        }

        private class TestTimerEventHandler : ILimitStateMachineEvents
        {
            public List<(LimitState old, LimitState @new, string reason)> StateChanges { get; } = new();

            public void OnStateChanged(LimitState oldState, LimitState newState, string reason)
            {
                StateChanges.Add((oldState, newState, reason));
            }

            public void OnEffectiveLimitChanged(EffectiveLimit newLimit) { }
            public void OnFailsafeEntered(string reason) { }
            public void OnFailsafeExited(string reason) { }
        }
    }
}
