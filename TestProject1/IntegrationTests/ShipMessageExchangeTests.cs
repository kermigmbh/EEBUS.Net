using EEBUS;
using EEBUS.Net;
using EEBUS.SHIP.Messages;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace TestProject1.IntegrationTests
{
    public class ShipMessageExchangeTests : EebusIntegrationTests
    {
        public ShipMessageExchangeTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task WHEN_ServerDoesNotAnswerInitMessageInTime_THEN_ClientClosesConnection()
        {
            ILogger manager1Logger = GetLogger("CEM", minLogLevel: LogLevel.Debug);
            ILogger manager2Logger = GetLogger("Controlbox");
            using EEBUSManager manager1 = new EEBUSManager(Setup.GetCEMSettings(), logger: manager1Logger);
            using EEBUSManager manager2 = new EEBUSManager(Setup.GetControlBoxSettings(withPeriodicReconnect: false), logger: manager2Logger);

            manager2.BeforeMessageSent = async (message, connection) =>
            {
                if (message is InitMessage)
                {
                    await Task.Delay(45_000);
                }
                return true;
            };
            string manager1Ski = manager1.GetLocalData().SKI;
            string manager2Ski = manager2.GetLocalData().SKI;

            manager1.AddTrustedSki(manager2Ski);
            manager2.AddTrustedSki(manager1Ski);


            using var stateChangedWaiter = new TestWaiter<StateChangedEventArgs>(
               subscribe: handler => manager1.ClientStateChanged += handler,
               unsubscribe: handler => manager1.ClientStateChanged -= handler);

            await StartManagersAsync(manager1, manager2);

            await stateChangedWaiter.Match(args => args.State == Connection.EState.ErrorOrTimeout, 60_000);
            Log("Client Timed out and closed connection as expected.");
        }
    }
}
