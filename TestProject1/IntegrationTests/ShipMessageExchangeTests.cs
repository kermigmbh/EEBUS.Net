using EEBUS;
using EEBUS.Enums;
using EEBUS.Net;
using EEBUS.SHIP.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
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
            FakeTimeProvider timeProvider = new FakeTimeProvider();
            using EEBUSManager manager1 = new EEBUSManager(fileSystem: FileSystem, timeProvider: timeProvider, settings: Setup.GetCEMSettings(), logger: manager1Logger);
            using EEBUSManager manager2 = new EEBUSManager(fileSystem: FileSystem, timeProvider: timeProvider, settings: Setup.GetControlBoxSettings(withPeriodicReconnect: false), logger: manager2Logger);

            // Server holds back its init answer until the fake time was shifted forward, so the client runs into the CMI timeout.
            TaskCompletionSource initHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource timeAdvanced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            manager2.BeforeMessageSent = async (message, connection) =>
            {
                if (message is InitMessage)
                {
                    initHeld.TrySetResult();
                    await timeAdvanced.Task;
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

            try
            {
                await StartManagersAsync(manager1, manager2);
               // Wait until the connection is established and the server holds its init answer (discovery can take > 1 s).
                await initHeld.Task.WaitAsync(TimeSpan.FromSeconds(30));
                // Give the client time to enter its receive call so the timeout timer is registered.
                await Task.Delay(1000);
                timeProvider.Advance(TimeSpan.FromMilliseconds(SHIPMessageTimeout.CMI_TIMEOUT + 1));

                await stateChangedWaiter.Match(args => args.State == Connection.EState.ErrorOrTimeout, 10_000);
                Log("Client Timed out and closed connection as expected.");
            }
            finally
            {
                // Only now let the server send its init (return true), otherwise it could still reach the client before the timeout is processed.
                timeAdvanced.TrySetResult();
            }
        }
    }
}
