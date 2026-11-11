using EEBUS.Messages;
using EEBUS.Net.Connections;
using EEBUS.SHIP.Messages;
using EEBUS.UseCases.ControllableSystem;
using System.Text.Json.Nodes;

namespace EEBUS.Net.Handlers;

/// <summary>
/// Broadcasts notifications of the local device to all clients that subscribed to the corresponding local feature.
/// </summary>
internal sealed class NotifyEventHandler(ConnectionManager connections) : NotifyEvents
{
    public Task NotifyAsync(JsonNode? payload, AddressType localFeatureAddress)
    {
        if (payload == null) return Task.CompletedTask;

        foreach (Connection connection in connections.ActiveConnections)
        {
            foreach (AddressType clientAddress in connection.BindingAndSubscriptionManager.GetSubscriptionsByServerAddress(localFeatureAddress, BindingSubscriptionDirection.Incoming))
            {
                SpineDatagramPayload reply = new();
                reply.datagram.header.addressSource = localFeatureAddress;
                reply.datagram.header.addressDestination = clientAddress;
                reply.datagram.header.msgCounter = DataMessage.NextCount;
                reply.datagram.header.cmdClassifier = "notify";
                reply.datagram.payload = payload;

                DataMessage dataMessage = new();
                dataMessage.SetPayload(JsonHelper.ToJsonNode(reply) ?? throw new InvalidOperationException("Failed to serialize data message"));
                connection.PushDataMessage(dataMessage);
            }
        }
        return Task.CompletedTask;
    }
}
