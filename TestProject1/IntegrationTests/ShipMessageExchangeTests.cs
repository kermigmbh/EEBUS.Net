using EEBUS;
using EEBUS.Net;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
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
            using EEBUSManager manager2 = new EEBUSManager(Setup.GetControlBoxSettings(), logger: manager2Logger);
            manager1.BeforeMessageSent = async (message, connection) =>
            {
                if (connection is Server && message is InitMessage)
                {
                    await Task.Delay(45_000);
                }
                return true;
            };
            manager2.BeforeMessageSent = async (message, connection) =>
            {
                if (connection is Server && message is InitMessage)
                {
                    await Task.Delay(45_000);
                }
                return true;
            };
            string manager1Ski = manager1.GetLocalData().SKI;
            string manager2Ski = manager2.GetLocalData().SKI;

            manager1.AddTrustedSki(manager2Ski);
            manager2.AddTrustedSki(manager1Ski);

            await StartManagersAsync(manager1, manager2);
            await Task.Delay(60_000);
        }
    }
}
