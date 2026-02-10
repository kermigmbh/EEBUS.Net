using EEBUS.Models;

namespace EEBUS.UseCases.ControllableSystem
{
	public interface LPCEvents : UseCaseEvents
	{
		Task DataUpdateLimitAsync( int counter, bool active, long limit, TimeSpan duration );

        Task DataUpdateFailsafeConsumptionActivePowerLimitAsync( int counter, long limit );
	}

	public interface LPPEvents : UseCaseEvents
	{
        Task DataUpdateLimitAsync( int counter, bool active, long limit, TimeSpan duration );

        Task DataUpdateFailsafeProductionActivePowerLimitAsync( int counter, long limit );
	}

	public interface LPCorLPPEvents : UseCaseEvents
	{
        Task DataUpdateFailsafeDurationMinimumAsync( int counter, TimeSpan duration );

        Task DataUpdateHeartbeatAsync( int counter, RemoteDevice device, uint timeout );
	}
}
