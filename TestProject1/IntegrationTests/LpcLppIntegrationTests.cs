using EEBUS.Net;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.StateMachines;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace TestProject1.IntegrationTests
{
    public class LpcLppIntegrationTests : EebusIntegrationTests
    {
        public LpcLppIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task Scenario1()
        {
            ILogger cemLogger = GetLogger("CEM");
            ILogger controlBoxLogger = GetLogger("ControlBox");
            using EEBUSManager cemManager = new EEBUSManager(Setup.GetCEMSettings(), logger: cemLogger);
            using EEBUSManager controlBoxManager = new EEBUSManager(Setup.GetControlBoxSettings(), logger: controlBoxLogger);

            await StartAndConnectManagersAsync(cemManager, controlBoxManager);

            using var dataChangedWaiter = new TestWaiter<DeviceData>(
               subscribe: handler => cemManager.OnDeviceDataChanged += handler,
               unsubscribe: handler => cemManager.OnDeviceDataChanged -= handler);

            Log("Waiting for heartbeat init...");
            await dataChangedWaiter.Match(data => data.Lpc != null && data.Lpc.LimitState != LimitState.Init, 10000);

            Log("Sending data");
            await controlBoxManager.WriteDataAsync(new DeviceData()
            {
                Lpc = new LpcLppData() { Limit = 1000, LimitActive = true }
            }, cemManager.GetLocalData().SKI);

            await dataChangedWaiter.Match(data => data.Lpc?.Limit == 1000, 3000);
        }

        [Fact]
        public async Task Scenario2()
        {

        }

        [Fact]
        public async Task Scenario3()
        {

        }

        [Fact]
        public async Task Scenario4()
        {

        }
    }
}
