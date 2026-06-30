using EEBUS;
using EEBUS.Models;
using EEBUS.Net;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.StateMachines;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit.Abstractions;

namespace TestProject1.IntegrationTests
{
    public class CommunicationTest : EebusIntegrationTests
    {
        public CommunicationTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TwoEEBUSDevicesConnectivityAsync()
        {
            ILogger manager1Logger = GetLogger("Manager1");
            ILogger manager2Logger = GetLogger("Manager2");
            using EEBUSManager manager1 = new EEBUSManager(Setup.GetCEMSettings(), logger: manager1Logger);
            using EEBUSManager manager2 = new EEBUSManager(Setup.GetControlBoxSettings(), logger: manager2Logger);

            manager1.OnDeviceDataChanged += deviceData =>
            {
                Log($"Manager1 new data: {deviceData.Lpc?.Limit}");
                return Task.CompletedTask;
            };
            manager2.OnDeviceDataChanged += deviceData =>
            {
                if (deviceData.Lpc != null)
                    Log($"Manager2 new data: {deviceData.Lpc?.Limit}");
                return Task.CompletedTask;
            };
            manager1.OnDeviceFound += (sender, device) =>
            {
                Log($"Manager1 found device: {device.Name}");
            };

            manager2.OnDeviceFound += (sender, device) =>
            {
                Log($"Manager2 found device: {device.Name}");
            };

            manager1.OnDeviceConnectionStatusChanged += (remoteDevice, status) =>
            {
                Log($"Manager1 connection status changed: {remoteDevice.Name} is now {status}");
                return Task.CompletedTask;
            };
            
            manager2.OnDeviceConnectionStatusChanged += (remoteDevice, status) =>
            {
                Log($"Manager2 connection status changed: {remoteDevice.Name} is now {status}");
                return Task.CompletedTask;
            };

            string manager2Ski = manager2.GetLocalData().SKI;
            string manager1Ski = manager1.GetLocalData().SKI;
            manager1.AddTrustedSki(manager2Ski);
            manager2.AddTrustedSki(manager1Ski);

            await StartManagersAsync(manager1, manager2);

            using var manager2ReadyWaiter = new TestWaiter<RemoteDevice, DeviceConnectionStatus>(
                subscribe: handler => manager2.OnDeviceConnectionStatusChanged += handler,
                unsubscribe: handler => manager2.OnDeviceConnectionStatusChanged -= handler);
            using var manager1ReadyWaiter = new TestWaiter<RemoteDevice, DeviceConnectionStatus>(
                subscribe: handler => manager1.OnDeviceConnectionStatusChanged += handler,
                unsubscribe: handler => manager1.OnDeviceConnectionStatusChanged -= handler);

            bool connected = await manager1.TryConnectAsync(manager2.GetLocalData().SKI);
            if (connected == false)
            {
                Log("Failed to establish connection after multiple attempts.");
                throw new Exception("Failed to establish connection after multiple attempts.");
            }
            Log("manager1 -> manager2: connection OK");

            await manager2ReadyWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == manager1Ski);
            Log("Manager2 connection to manager1 is ready (UseCaseDiscoveryCompleted).");

            await manager1ReadyWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == manager2Ski);
            Log("Manager1 is ready (UseCaseDiscoveryCompleted).");


            using var dataChangedWaiter = new TestWaiter<DeviceData>(
                subscribe: handler => manager1.OnDeviceDataChanged += handler,
                unsubscribe: handler => manager1.OnDeviceDataChanged -= handler);


            Log("Waiting for heartbeat init...");
            await dataChangedWaiter.Match(data => data.Lpc != null && data.Lpc.LimitState != LimitState.Init, 10000);

            Log("Sending data");
            await manager2.WriteDataAsync(new DeviceData()
            {
                Lpc = new LpcLppData() { Limit = 1000, LimitActive = true }
            }, manager1.GetLocalData().SKI);

            await dataChangedWaiter.Match(data => data.Lpc?.Limit == 1000, 3000);


            Log("Data received!");

        }

        [Fact]
        public async Task WHEN_SkiNotRegisteredAsTrusted_THEN_ConnectionFailsAsync()
        {
            ILogger manager1Logger = GetLogger("Manager1");
            ILogger manager2Logger = GetLogger("Manager2");
            using EEBUSManager manager1 = new EEBUSManager(Setup.GetCEMSettings(), logger: manager1Logger);
            using EEBUSManager manager2 = new EEBUSManager(Setup.GetControlBoxSettings(), logger: manager2Logger);

            await StartManagersAsync(manager1, manager2);

            bool connected = await manager1.TryConnectAsync(manager2.GetLocalData().SKI);
            Assert.False(connected, "Connection should fail when SKI is not registered as trusted.");

            manager1.AddTrustedSki(manager2.GetLocalData().SKI);
            manager2.AddTrustedSki(manager1.GetLocalData().SKI);

            connected = await manager1.TryConnectAsync(manager2.GetLocalData().SKI);
            Assert.True(connected, "Connection should succeed after SKI is registered as trusted.");
        }

        //[Fact]
        //public async Task WHEN_LimitIsUpdated_THEN_ControlBoxIsNotified()
        //{
        //    //For this test we would need to implement the notify path in LoadControlLimitListData

        //    ILogger manager1Logger = GetLogger("Manager1");
        //    ILogger manager2Logger = GetLogger("Manager2");
        //    EEBUSManager manager1 = new EEBUSManager(Setup.GetCEMSettings(), logger: manager1Logger);
        //    EEBUSManager manager2 = new EEBUSManager(Setup.GetControlBoxSettings(), logger: manager2Logger);

        //    string manager2Ski = manager2.GetLocalData().SKI;
        //    string manager1Ski = manager1.GetLocalData().SKI;
        //    manager1.AddTrustedSki(manager2Ski);
        //    manager2.AddTrustedSki(manager1Ski);
        //    await StartManagersAsync(manager1, manager2);

        //    bool connected = await manager1.TryConnectAsync(manager2.GetLocalData().SKI);
        //    Assert.True(connected, "Connection should succeed after SKI is registered as trusted.");

        //    using var dataChangedWaiter = new TestWaiter<DeviceData>(
        //        subscribe: handler => manager2.OnDeviceDataChanged += handler,
        //        unsubscribe: handler => manager2.OnDeviceDataChanged -= handler);
        //    Log("Sending data");
        //    await manager1.WriteDataAsync(new DeviceData()
        //    {
        //        Lpc = new LpcLppData() { Limit = 2000, LimitActive = true }
        //    }, manager2.GetLocalData().SKI);
        //    await dataChangedWaiter.Match(data => data.Lpc?.Limit == 2000, 3000);
        //    Log("ControlBox received updated limit!");
        //}
    }
}
