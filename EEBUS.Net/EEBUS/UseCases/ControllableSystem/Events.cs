using EEBUS.Messages;
using EEBUS.Models;
using System.Text.Json.Nodes;

namespace EEBUS.UseCases.ControllableSystem
{
	public interface LPCEvents : UseCaseEvents
	{
		Task DataUpdateLimitAsync( int counter, bool active, long limit, TimeSpan duration, string remoteSki);

        Task DataUpdateFailsafeConsumptionActivePowerLimitAsync( int counter, long limit, string remoteSki);
	}

	public interface LPPEvents : UseCaseEvents
	{
        Task DataUpdateLimitAsync( int counter, bool active, long limit, TimeSpan duration, string remoteSki);

        Task DataUpdateFailsafeProductionActivePowerLimitAsync( int counter, long limit, string remoteSki);
	}

	public interface LPCorLPPEvents : UseCaseEvents
	{
        Task DataUpdateFailsafeDurationMinimumAsync( int counter, TimeSpan duration, string remoteSki);

        Task DataUpdateHeartbeatAsync( int counter, RemoteDevice device, uint timeout, string remoteSki);
	}

    public interface NotifyEvents : UseCaseEvents
    {
        Task NotifyAsync(JsonNode? payload, AddressType localFeatureAddress);
    }

	public interface DeviceConnectionStatusEvents : UseCaseEvents
	{
		Task RemoteDiscoveryCompletedAsync(RemoteDevice remoteDevice);
	}
}
