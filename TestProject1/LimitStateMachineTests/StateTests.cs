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
    public class StateTests : LpcTestRunner
    {
        public StateTests() : base()
        {
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
            Assert.Equal(DefaultFailsafeLimit, limit.Value);
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
        public async Task OnHeartbeatReceived_ShouldSetHasReceivedHeartbeat()
        {
            await NotifyHeartbeat();

            Assert.True(_stateMachine.HasReceivedHeartbeat);
        }
        #endregion

        #region Effective Limit Tests

        [Fact]
        public async Task GetEffectiveLimit_InUnlimitedControlled_ShouldReturnUnlimited()
        {
            // Arrange: Get to UnlimitedControlled state
            var request = WriteRequest(false, 0, null);
            await NotifyHeartbeat();
            await WriteLimit(request);

            // Act
            var limit = _stateMachine.GetEffectiveLimit();

            // Assert
            Assert.False(limit.IsLimited);
            Assert.Equal(long.MaxValue, limit.Value);
            Assert.Equal("none", limit.Source);
        }

        [Fact]
        public async Task GetEffectiveLimit_InLimited_ShouldReturnActiveLimit()
        {
            // Arrange: Get to Limited state with 7500W limit
            var request = WriteRequest(true, 7500, null);
            await NotifyHeartbeat();
            await WriteLimit(request);

            // Act
            var limit = _stateMachine.GetEffectiveLimit();

            // Assert
            Assert.True(limit.IsLimited);
            Assert.Equal(7500, limit.Value);
            Assert.Equal("active", limit.Source);
            Assert.Equal(LimitState.Limited, limit.State);
        }

        [Fact]
        public async Task GetEffectiveLimit_WithScale_ShouldCalculateCorrectValue()
        {
            // Arrange: 
            var request = WriteRequest(true, 5000, null);
            await NotifyHeartbeat();
            await WriteLimit(request);

            // Act
            var limit = _stateMachine.GetEffectiveLimit();

            // Assert
            Assert.Equal(5000, limit.Value);
        }

        #endregion

        #region Auto-Reject Tests

        [Fact]
        public async Task EvaluateLimitWrite_NegativeValue_ShouldReject()
        {
            // Arrange
            var request = WriteRequest(true, -1000, null);

            // Act
            await NotifyHeartbeat();
            var result = await _stateMachine.ApproveActiveLimitWriteAsync(request);

            // Assert
            Assert.False(result.Approved);
            Assert.Contains("positive", result.Description, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task EvaluateLimitWrite_ValidValue_ShouldAllow()
        {
            // Arrange
            var request = WriteRequest(true, 5000, null);

            // Act
            await NotifyHeartbeat();
            var result = await _stateMachine.ApproveActiveLimitWriteAsync(request);

            // Assert
            Assert.True(result.Approved);
        }

        [Fact]
        public async Task EvaluateLimitWrite_ZeroValue_ShouldAllow()
        {
            // Arrange
            var request = WriteRequest(true, 0, null);

            // Act
            await NotifyHeartbeat();
            var result = await _stateMachine.ApproveActiveLimitWriteAsync(request);

            // Assert
            Assert.True(result.Approved);
        }

        #endregion

        #region Failsafe Configuration Tests

        [Fact]
        public async Task SetFailsafeLimit_ShouldUpdateEffectiveLimit_WhenInInitState()
        {
            // Act
            await _stateMachine.DataUpdateFailsafeActivePowerLimitAsync(2000);

            // Assert
            var limit = _stateMachine.GetEffectiveLimit();
            Assert.Equal(2000, limit.Value);
        }

        [Fact]
        public void SetFailsafeDuration_ShouldUpdateDuration()
        {
            // Act
            _stateMachine.DataUpdateFailsafeDurationMinimumAsync(0, TimeSpan.FromHours(4), _remoteSki);

            // Assert
            Assert.Equal(TimeSpan.FromHours(4), _stateMachine.FailsafeDurationMinimum);
        }

        #endregion

        #region Event Handler Tests

        [Fact]
        public async Task StateChange_ShouldFireOnStateChangedEvent()
        {
            // Arrange
            var request = WriteRequest(true, 5000, null);

            // Act
            await NotifyHeartbeat();
            await WriteLimit(request);

            // Assert
            Assert.Equal(2U, _eventHandler.StateChangedEventCount);
            Assert.Equal(LimitState.InitPlusHeartbeat, _eventHandler.LastOldState);
            Assert.Equal(LimitState.Limited, _eventHandler.LastNewState);
        }

        [Fact]
        public async Task StateChange_ShouldFireOnEffectiveLimitChangedEvent()
        {
            // Arrange
            var request = WriteRequest(true, 5000, null);

            // Act
            await NotifyHeartbeat();
            await WriteLimit(request);

            // Assert
            Assert.Equal(1U, _eventHandler.EffectiveLimitChangedEventCalled);
            Assert.NotNull(_eventHandler.LastEffectiveLimit);
            Assert.Equal(5000, _eventHandler.LastEffectiveLimit.Value);
        }

        [Fact]
        public async Task UnregisterEventHandler_ShouldStopReceivingEvents()
        {
            // Arrange
            _stateMachine.UnregisterEventHandler(_eventHandler);
            var request = WriteRequest(true, 5000, null);

            // Act
            await NotifyHeartbeat();
            await WriteLimit(request);

            // Assert
            Assert.Equal(0U, _eventHandler.StateChangedEventCount);
        }

        #endregion

        #region Direction Tests

        [Fact]
        public void Direction_ShouldMatchConstructorParameter()
        {
            Assert.Equal(PowerDirection.Consumption, _stateMachine.Direction);

            using var productionMachine = new LppLimitStateMachine(500);
            Assert.Equal(PowerDirection.Production, productionMachine.Direction);
        }

        #endregion

        #region Limit Update in Limited State

        [Fact]
        public async Task LimitUpdate_InLimitedState_ShouldUpdateValue()
        {
            // Arrange: Get to Limited state with 5000W
            var request1 = WriteRequest( true, 5000, null);
            await NotifyHeartbeat();
            await WriteLimit(request1);
            Assert.Equal(5000, _stateMachine.GetEffectiveLimit().Value);

            // Act: Update to 3000W
            var request2 = WriteRequest(true, 3000, null);
            await WriteLimit(request2);

            // Assert
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
            Assert.Equal(3000, _stateMachine.GetEffectiveLimit().Value);
        }

        #endregion
    }
}
