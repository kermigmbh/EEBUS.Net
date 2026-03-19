using EEBUS.Models;
using EEBUS.StateMachines;
using EEBUS.UseCases;
using Microsoft.Extensions.Time.Testing;

namespace TestProject1
{
    /// <summary>
    /// Tests for timer-based state transitions in LimitStateMachine.
    /// These tests use shorter timeouts for faster execution.
    /// </summary>
    public class LimitStateMachineTimerTests : IDisposable
    {
        private readonly FakeTimeProvider _timeProvider;
        private LimitStateMachine _stateMachine;
        private TestTimerEventHandler _eventHandler;
        private static string _remoteSki = "9EB90FCE71E9D0705102EA55555593F9DA95FB6F";
        private static RemoteDevice _mockRemoteDevice = new RemoteDevice("", _remoteSki, "", "", (x, y) => { }, (x, y) => { });

        public LimitStateMachineTimerTests()
        {
            _timeProvider = new()
            {
                AutoAdvanceAmount = TimeSpan.FromMilliseconds(1)
            };

            _stateMachine = new LpcLimitStateMachine(_timeProvider, 1000);
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

        private async Task AdvanceTimeMaintainingState(TimeSpan duration, LimitState expectedState)
        {
            var heartbeatInterval = LpcLimitStateMachine.HeartbeatAcceptTimeout.Divide(2);
            int numHeartbeats = (int) duration.Divide(heartbeatInterval);

            // Act: Advance Time
            for (int i = 0; i < numHeartbeats; i++)
            {
                Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
                _timeProvider.Advance(heartbeatInterval);
                await NotifyHeartbeat();
            }
        }

        #endregion

        [Fact]
        public async Task Transition6_LimitDurationExpired_ShouldTransitionToUnlimitedControlled()
        {
            // Arrange: Create state machine and get to Limited state with short duration
            _stateMachine.RegisterEventHandler(_eventHandler);

            TimeSpan limitDuration = TimeSpan.FromMinutes(15);
            var request = new ActiveLimitWriteRequest(
                PowerDirection.Consumption,
                isActive: true,
                value: 5000,
                duration: limitDuration,
                remoteDeviceId: "test",
                remoteSKI: _remoteSki
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
            await AdvanceTimeMaintainingState(TimeSpan.FromDays(2), LimitState.Limited);

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
            await AdvanceTimeMaintainingState(TimeSpan.FromDays(2), LimitState.Limited);

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
            Assert.True(limit.ExpiresAt > _timeProvider.GetUtcNow());
            Assert.True(limit.ExpiresAt < _timeProvider.GetUtcNow().AddHours(2));
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
            var limitDuration1 = TimeSpan.FromMinutes(5);
            var request1 = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 5000,
                limitDuration1, "test", "test");

            await NotifyHeartbeat();
            await WriteLimit(request1);

            // Act: After 2 minutes, send new limit with new duration
            await AdvanceTimeMaintainingState(TimeSpan.FromMinutes(2), LimitState.Limited);
            var limitDuration2 = TimeSpan.FromMinutes(15);
            var request2 = new ActiveLimitWriteRequest(
                PowerDirection.Consumption, true, 3000,
                limitDuration2, "test", "test");
            await WriteLimit(request2);

            // Wait past original timeout but before new timeout
            await AdvanceTimeMaintainingState(TimeSpan.FromMinutes(10), LimitState.Limited);

            // Assert: Should still be Limited (timer was reset)
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
            Assert.Equal(3000, _stateMachine.GetEffectiveLimit().Value);

            // Wait for new duration to expire
            await AdvanceTimeMaintainingState(TimeSpan.FromMinutes(5), LimitState.Limited);
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

            public Task<WriteApprovalResult> ApproveFailsafeDurationMinimumWriteAsync(FailsafeDurationWriteRequest request)
            {
                return Task.FromResult(WriteApprovalResult.Accept());
            }
        }
    }
}
