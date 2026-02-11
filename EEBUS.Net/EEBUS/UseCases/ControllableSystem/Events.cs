using EEBUS.Models;

namespace EEBUS.UseCases.ControllableSystem
{
	public interface LPCEvents : UseCaseEvents
	{
		// Write Approval methods (called BEFORE write is applied)
		WriteApprovalResult ApproveActiveLimitWrite( ActiveLimitWriteRequest request );

		WriteApprovalResult ApproveFailsafeLimitWrite( FailsafeLimitWriteRequest request );

		// Data update events (called AFTER approved write is applied)
		void DataUpdateLimit( int counter, bool active, long limit, TimeSpan duration );

		void DataUpdateFailsafeConsumptionActivePowerLimit( int counter, long limit );
	}

	public interface LPPEvents : UseCaseEvents
	{
		// Write Approval methods
		WriteApprovalResult ApproveActiveLimitWrite( ActiveLimitWriteRequest request );

		WriteApprovalResult ApproveFailsafeLimitWrite( FailsafeLimitWriteRequest request );

		// Data update events
		void DataUpdateLimit( int counter, bool active, long limit, TimeSpan duration );

		void DataUpdateFailsafeProductionActivePowerLimit( int counter, long limit );
	}

	public interface LPCorLPPEvents : UseCaseEvents
	{
		// Write Approval method
		WriteApprovalResult ApproveFailsafeDurationWrite( FailsafeDurationWriteRequest request );

		// Data update events
		void DataUpdateFailsafeDurationMinimum( int counter, TimeSpan duration );

		void DataUpdateHeartbeat( int counter, RemoteDevice device, uint timeout );
	}
}
