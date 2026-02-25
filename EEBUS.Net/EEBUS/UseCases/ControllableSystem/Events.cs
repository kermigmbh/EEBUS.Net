using EEBUS.Messages;
using EEBUS.Models;
using System.Text.Json.Nodes;

namespace EEBUS.UseCases.ControllableSystem
{
	public interface LPCEvents : UseCaseEvents
	{
		// Write Approval methods (called BEFORE write is applied)
		Task<WriteApprovalResult> ApproveActiveLimitWriteAsync( ActiveLimitWriteRequest request );

		Task<WriteApprovalResult> ApproveFailsafeLimitWriteAsync( FailsafeLimitWriteRequest request );

		// Data update events (called AFTER approved write is applied)
		Task DataUpdateLimitAsync( int counter, bool active, long limit, TimeSpan duration, string remoteSki );

		Task DataUpdateFailsafeConsumptionActivePowerLimitAsync( int counter, long limit, string remoteSki );
	}

	public interface LPPEvents : UseCaseEvents
	{
		// Write Approval methods
		Task<WriteApprovalResult> ApproveActiveLimitWriteAsync( ActiveLimitWriteRequest request );

		Task<WriteApprovalResult> ApproveFailsafeLimitWriteAsync( FailsafeLimitWriteRequest request );

		// Data update events
		Task DataUpdateLimitAsync( int counter, bool active, long limit, TimeSpan duration, string remoteSki );

		Task DataUpdateFailsafeProductionActivePowerLimitAsync( int counter, long limit, string remoteSki );
	}

	public interface LPCorLPPEvents : UseCaseEvents
	{
		// Write Approval method
		Task<WriteApprovalResult> ApproveFailsafeDurationWriteAsync( FailsafeDurationWriteRequest request );

		// Data update events
		Task DataUpdateFailsafeDurationMinimumAsync( int counter, TimeSpan duration, string remoteSki );

		Task DataUpdateHeartbeatAsync( int counter, RemoteDevice device, uint timeout, string remoteSki );
	}

    public interface NotifyEvents : UseCaseEvents
    {
        Task NotifyAsync(JsonNode? payload, AddressType localFeatureAddress);
    }

	public interface DeviceConnectionStatusEvents : UseCaseEvents
	{
		Task DeviceConnectionStatusUpdatedAsync(Connection connection);
	}
}
