using EEBUS.StateMachines;
using EEBUS.UseCases;

namespace TestProject1.LimitStateMachineTests
{
    public class TestEventHandler : ILimitStateMachineEvents
    {
        public LimitState? LastOldState { get; private set; }
        public LimitState? LastNewState { get; private set; }
        public EffectiveLimit? LastEffectiveLimit { get; private set; }
        public uint StateChangedEventCount { get; private set; }
        public uint EffectiveLimitChangedEventCalled { get; private set; }
        public uint FailsafeEnteredEventCount { get; private set; }
        public uint FailsafeExitedEventCount { get; private set; }

        public Task OnStateChanged(LimitState oldState, LimitState newState, string reason)
        {
            LastOldState = oldState;
            LastNewState = newState;
            StateChangedEventCount++;

            return Task.CompletedTask;
        }

        public Task OnEffectiveLimitChanged(EffectiveLimit newLimit)
        {
            LastEffectiveLimit = newLimit;
            EffectiveLimitChangedEventCalled++;

            return Task.CompletedTask;
        }

        public Task OnFailsafeEntered(string reason)
        {
            FailsafeEnteredEventCount++;
            return Task.CompletedTask;
        }

        public Task OnFailsafeExited(string reason)
        {
            FailsafeExitedEventCount++;
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