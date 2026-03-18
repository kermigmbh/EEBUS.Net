using EEBUS.Models;
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
        private LimitStateMachine _stateMachine;
        private TestTimerEventHandler _eventHandler;
        private static string _remoteSki = "9EB90FCE71E9D0705102EA55555593F9DA95FB6F";
        private static RemoteDevice _mockRemoteDevice = new RemoteDevice("", _remoteSki, "", "", (x, y) => { }, (x, y) => { });

        public LimitStateMachineTimerTests()
        {
            _stateMachine = new LpcLimitStateMachine(1000);
            _eventHandler = new TestTimerEventHandler();
            _stateMachine.RegisterEventHandler(_eventHandler);
        }

        public void Dispose()
        {
            _stateMachine?.Dispose();
        }

        #region Helper Functions

        private async Task NotifyHeartbeat()
        {
            await _stateMachine.DataUpdateHeartbeatAsync(0, _mockRemoteDevice, 0, "");
        }

        private async Task WriteLimit(ActiveLimitWriteRequest request)
        {
            var result = await _stateMachine.ApproveActiveLimitWriteAsync(request);
            Assert.True(result.Approved);
            await _stateMachine.DataUpdateLimitAsync(1, request.IsLimitActive, request.Value, request.Duration ?? Timeout.InfiniteTimeSpan, _remoteSki);
        }

        #endregion

        [Fact]
        public async Task Transition6_LimitDurationExpired_ShouldTransitionToUnlimitedControlled()
        {
            // Arrange: Create state machine and get to Limited state with short duration
            _stateMachine.RegisterEventHandler(_eventHandler);

            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption,
                isActive: true,
                value: 5000,
                duration: TimeSpan.FromMilliseconds(100), // Very short duration for testing
                remoteDeviceId: "test",
                remoteSKI: _remoteSki
            );

            await NotifyHeartbeat();
            await WriteLimit(request);
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
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption,
                isActive: true,
                value: 5000,
                duration: null, // No duration
                remoteDeviceId: "test",
                remoteSKI: "test"
            );

            await NotifyHeartbeat();
            await WriteLimit(request);

            // Act: Wait some time
            await Task.Delay(200);

            // Assert: Should still be Limited
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
            Assert.True(_stateMachine.GetEffectiveLimit().IsLimited);
            Assert.Equal(5000, _stateMachine.GetEffectiveLimit().Value);
        }

        [Fact]
        public async Task LimitWithInfiniteDuration_ShouldNotExpire()
        {
            // Arrange
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption,
                isActive: true,
                value: 5000,
                duration: Timeout.InfiniteTimeSpan,
                remoteDeviceId: "test",
                remoteSKI: "test"
            );

            await NotifyHeartbeat();
            await WriteLimit(request);

            // Act: Wait some time
            await Task.Delay(200);

            // Assert: Should still be Limited
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
        }

        [Fact]
        public async Task EffectiveLimit_ExpiresAt_ShouldBeSetWhenDurationProvided()
        {
            // Arrange
            var duration = TimeSpan.FromHours(1);

            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption,
                isActive: true,
                value: 5000,
                duration: duration,
                remoteDeviceId: "test",
                remoteSKI: "test"
            );

            // Act
            await NotifyHeartbeat();
            await WriteLimit(request);

            // Assert
            var limit = _stateMachine.GetEffectiveLimit();
            Assert.NotNull(limit.ExpiresAt);
            Assert.True(limit.ExpiresAt > DateTimeOffset.UtcNow);
            Assert.True(limit.ExpiresAt < DateTimeOffset.UtcNow.AddHours(2));
        }

        [Fact]
        public async Task EffectiveLimit_ExpiresAt_ShouldBeNullWhenNoDuration()
        {
            // Arrange
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption,
                isActive: true,
                value: 5000,
                duration: null,
                remoteDeviceId: "test",
                remoteSKI: "test"
            );

            // Act
            await NotifyHeartbeat();
            await WriteLimit(request);

            // Assert
            var limit = _stateMachine.GetEffectiveLimit();
            Assert.Null(limit.ExpiresAt);
        }

        [Fact]
        public async Task NewLimitWrite_ShouldResetDurationTimer()
        {
            // Arrange
            var request1 = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 5000,
                TimeSpan.FromMilliseconds(500), "test", "test");

            await NotifyHeartbeat();
            await WriteLimit(request1);

            // Act: After 50ms, send new limit with new duration
            await Task.Delay(50);
            var request2 = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 3000,
                TimeSpan.FromMilliseconds(1500), "test", "test");
            await WriteLimit(request2);

            // Wait past original timeout but before new timeout
            await Task.Delay(200);

            // Assert: Should still be Limited (timer was reset)
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
            Assert.Equal(3000, _stateMachine.GetEffectiveLimit().Value);

            // Wait for new duration to expire
            await Task.Delay(500);
            Assert.Equal(LimitState.UnlimitedControlled, _stateMachine.CurrentState);
        }

        private class TestTimerEventHandler : ILimitStateMachineEvents
        {
            public List<(LimitState old, LimitState @new, string reason)> StateChanges { get; } = new();

            public Task OnStateChanged(LimitState oldState, LimitState newState, string reason)
            {
                StateChanges.Add((oldState, newState, reason));
                return Task.CompletedTask;
            }

            public Task OnEffectiveLimitChanged(EffectiveLimit newLimit)
            {
                return Task.CompletedTask;
            }

            public Task<WriteApprovalResult> ApproveActiveLimitWriteAsync(ActiveLimitWriteRequest request)
            {
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public Task<WriteApprovalResult> ApproveFailsafeLimitWriteAsync(FailsafeLimitWriteRequest request)
            {
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public Task<WriteApprovalResult> ApproveFailsafeMinimumDurationWriteAsync(FailsafeDurationWriteRequest request)
            {
                return Task.FromResult(WriteApprovalResult.Accept());
            }
        }
    }
}
