using EEBUS.Models;

namespace EEBUS.UseCases.ControllableSystem
{
	public interface LPCEvents : UseCaseEvents
	{
		void DataUpdateLimit( int counter, bool active, long limit, TimeSpan duration );

		void DataUpdateFailsafeConsumptionActivePowerLimit( int counter, long limit );
	}

	public interface LPPEvents : UseCaseEvents
	{
		void DataUpdateLimit( int counter, bool active, long limit, TimeSpan duration );

		void DataUpdateFailsafeProductionActivePowerLimit( int counter, long limit );
	}

	public interface LPCorLPPEvents : UseCaseEvents
	{
		void DataUpdateFailsafeDurationMinimum( int counter, TimeSpan duration );

		void DataUpdateHeartbeat( int counter, RemoteDevice device, uint timeout );
	}
}
