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
    public class TimerTests : LpcTestRunner
    {
        public TimerTests() : base()
        {
        }

        [Fact]
        public async Task LimitWithNoDuration_ShouldNotExpire()
        {
            // Arrange
            var request = WriteRequest(
                isActive: true,
                value: 5000,
                duration: null // No duration
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
    }
}
