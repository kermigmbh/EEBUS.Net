using EEBUS.UseCases.ControllableSystem;
using Microsoft.Extensions.Logging;

namespace EEBUS.Net.Handlers;

/// <summary>
/// Drives the discovery flow of a connection: Unknown -> NodeDiscoveryCompleted -> UseCaseDiscoveryCompleted.
/// </summary>
internal sealed class DeviceConnectionStatusEventHandler(ILogger? logger) : DeviceConnectionStatusEvents
{
    public Task DeviceConnectionStatusUpdatedAsync(Connection connection)
    {
        logger?.LogDebug("Status Changed for device {ski}: {status}", connection.Remote?.SKI.ToString(), connection.ConnectionStatus.ToString());

        switch (connection.ConnectionStatus)
        {
            case DeviceConnectionStatus.UseCaseDiscoveryCompleted:
                connection.ReadAndSubscribe();
                break;
            case DeviceConnectionStatus.NodeDiscoveryCompleted:
                connection.SendUseCaseDiscoveryRead();  //We need the node information first (entities, features)
                break;
        }
        return Task.CompletedTask;
    }
}
