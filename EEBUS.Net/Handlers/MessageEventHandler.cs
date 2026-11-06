using EEBUS.Messages;
using EEBUS.UseCases.ControllableSystem;

namespace EEBUS.Net.Handlers;

/// <summary>
/// Lets the consumer of <see cref="EEBUSManager"/> intercept outgoing messages.
/// </summary>
internal sealed class MessageEventHandler(EEBUSManager manager) : MessageEvents
{
    public Task<bool> BeforeMessageSentAsync(ShipMessageBase message, Connection connection)
        => manager.BeforeMessageSent?.Invoke(message, connection) ?? Task.FromResult(true);
}
