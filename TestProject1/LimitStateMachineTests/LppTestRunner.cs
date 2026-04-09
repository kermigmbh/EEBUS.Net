using EEBUS.Models;
using EEBUS.StateMachines;
using EEBUS.UseCases;
using Microsoft.Extensions.Time.Testing;

namespace TestProject1.LimitStateMachineTests;

public class LppTestRunner : IDisposable
{
    protected static readonly string _remoteSki = "9EB90FCE71E9D0705102EA55555593F9DA95FB6F";

    protected static readonly RemoteDevice
        _mockRemoteDevice = new("", _remoteSki, "", "", (x, y) => { }, (x, y) => { });

    protected static readonly long DefaultFailsafeLimit = 6666;
    protected readonly FakeTimeProvider _timeProvider;
    protected readonly LimitStateMachine _stateMachine;
    protected readonly TestEventHandler _eventHandler;
    private int _counter = 1;

    public LppTestRunner()
    {
        _timeProvider = new()
        {
            AutoAdvanceAmount = TimeSpan.FromMilliseconds(1)
        };

        _stateMachine = new LppLimitStateMachine(_timeProvider, DefaultFailsafeLimit);
        _eventHandler = new TestEventHandler();
        _stateMachine.RegisterEventHandler(_eventHandler);
    }

    protected int Counter
    {
        get => _counter++;
    }

    public void Dispose()
    {
        _stateMachine?.Dispose();
    }

    #region Helper Functions

    protected static ActiveLimitWriteRequest WriteRequest(bool isActive, long value, TimeSpan? duration)
    {
        return new ActiveLimitWriteRequest(
            PowerDirection.Production,
            isActive,
            value,
            duration,
            remoteDeviceId: "test",
            remoteSKI: _remoteSki
        );
    }

    protected async Task NotifyHeartbeat()
    {
        await _stateMachine.DataUpdateHeartbeatAsync(Counter, _mockRemoteDevice, 0, "");
    }

    protected async Task WriteLimit(ActiveLimitWriteRequest request, bool shouldApprove = true)
    {
        var result = await _stateMachine.ApproveActiveLimitWriteAsync(request);
        Assert.Equal(shouldApprove, result.Approved);
        
        if (result.Approved)
            await _stateMachine.DataUpdateLimitAsync(Counter, request.IsLimitActive, request.Value,
                request.Duration ?? Timeout.InfiniteTimeSpan, _remoteSki);
    }

    protected async Task AdvanceTimeMaintainingState(TimeSpan duration, LimitState expectedState)
    {
        var heartbeatInterval = LpcLimitStateMachine.HeartbeatAcceptTimeout.Divide(2);
        int numHeartbeats = (int)duration.Divide(heartbeatInterval);

        // Act: Advance Time
        for (int i = 0; i < numHeartbeats; i++)
        {
            Assert.Equal(LimitState.Limited, _stateMachine.CurrentState);
            _timeProvider.Advance(heartbeatInterval);
            await NotifyHeartbeat();
        }
    }

    protected void AdvanceTime(TimeSpan duration)
    {
        _timeProvider.Advance(duration);
    }

    protected async Task WriteLimitExpectingRejection(ActiveLimitWriteRequest request)
    {
        var result = await _stateMachine.ApproveActiveLimitWriteAsync(request);
        Assert.False(result.Approved, "Expected limit to be rejected");
    }

    protected async Task WriteFailsafeLimit(long limit)
    {
        await _stateMachine.DataUpdateFailsafeActivePowerLimitAsync(limit);
    }

    protected async Task WriteFailsafeDuration(TimeSpan duration)
    {
        await _stateMachine.DataUpdateFailsafeDurationMinimumAsync(Counter, duration, _remoteSki);
    }

    #endregion
}