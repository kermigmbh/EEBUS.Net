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

        [Fact]
        public async Task ReadMeasurementsTest()
        {
            ILogger eMeterMonitorLogger = GetLogger("E-MeterMonitor");
            ILogger eMeterLogger = GetLogger("E-Meter");

            MeasurementSettings initMeasurements = new MeasurementSettings
            {
                AcEnergyConsumed = 225
            };

            using EEBUSManager eMeterMonitorManager = new EEBUSManager(Setup.GetEMeterMonitorSettings(), logger: eMeterMonitorLogger);
            using EEBUSManager eMeterManager = new EEBUSManager(Setup.GetEMeterSettings(initMeasurements), logger: eMeterLogger);
            string emeterMonitorSki = eMeterMonitorManager.GetLocalData().SKI;
            string emeterSki = eMeterManager.GetLocalData().SKI;

            eMeterMonitorManager.AddTrustedSki(emeterSki);
            eMeterManager.AddTrustedSki(emeterMonitorSki);

            await StartManagersAsync(eMeterMonitorManager, eMeterManager);

            using var emeterManagerReadyWaiter = new TestWaiter<RemoteDevice, DeviceConnectionStatus>(
                subscribe: handler => eMeterManager.OnDeviceConnectionStatusChanged += handler,
                unsubscribe: handler => eMeterManager.OnDeviceConnectionStatusChanged -= handler);
            using var emeterMonitorManagerReadyWaiter = new TestWaiter<RemoteDevice, DeviceConnectionStatus>(
                subscribe: handler => eMeterMonitorManager.OnDeviceConnectionStatusChanged += handler,
                unsubscribe: handler => eMeterMonitorManager.OnDeviceConnectionStatusChanged -= handler);

            bool connected = await eMeterMonitorManager.TryConnectAsync(emeterSki);
            Assert.True(connected, "Connection should succeed.");

            await emeterMonitorManagerReadyWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == emeterSki && status == DeviceConnectionStatus.UseCaseDiscoveryCompleted);
            Log("Manager2 connection to manager1 is ready (UseCaseDiscoveryCompleted).");

            await emeterManagerReadyWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == emeterMonitorSki && status == DeviceConnectionStatus.UseCaseDiscoveryCompleted);
            Log("Manager1 is ready (UseCaseDiscoveryCompleted).");

            var data = new DeviceData
            {
                Measurements = new MeasurementsData()
            };

            Log("Reading data from E-Meter...");
            await eMeterMonitorManager.ReadDataAsync(data, eMeterManager.GetLocalData().SKI);

            Assert.Equal(225, data?.Measurements?.AcEnergyConsumed);
        }

        [Fact]
        public async Task ReadLimitTest()
        {
            ILogger cemLogger = GetLogger("CEM");
            ILogger controlBoxLogger = GetLogger("ControlBox");

            LimitSettings initLimits = new LimitSettings
            {
                Active = true,
                Duration = TimeSpan.FromHours(2),
                Limit = 2000,
                FailsafeDurationMinimum = TimeSpan.FromHours(2),
                FailsafeLimit = 1000,
                NominalMax = 3000
            };

            using EEBUSManager cemManager = new EEBUSManager(Setup.GetCEMSettings(initLimits), logger: cemLogger);
            using EEBUSManager controlBoxManager = new EEBUSManager(Setup.GetControlBoxSettings(), logger: controlBoxLogger);
            string cemSki = cemManager.GetLocalData().SKI;
            string controlBoxSki = controlBoxManager.GetLocalData().SKI;

            cemManager.AddTrustedSki(controlBoxSki);
            controlBoxManager.AddTrustedSki(cemSki);

            await StartManagersAsync(cemManager, controlBoxManager);

            using var cemMangerReadyWaiter = new TestWaiter<RemoteDevice, DeviceConnectionStatus>(
               subscribe: handler => cemManager.OnDeviceConnectionStatusChanged += handler,
               unsubscribe: handler => cemManager.OnDeviceConnectionStatusChanged -= handler);
            using var controlBoxReadyWaiter = new TestWaiter<RemoteDevice, DeviceConnectionStatus>(
                subscribe: handler => controlBoxManager.OnDeviceConnectionStatusChanged += handler,
                unsubscribe: handler => controlBoxManager.OnDeviceConnectionStatusChanged -= handler);

            bool connected = await controlBoxManager.TryConnectAsync(cemSki);
            Assert.True(connected, "Connection should succeed.");

            await controlBoxReadyWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == cemSki && status == DeviceConnectionStatus.UseCaseDiscoveryCompleted);
            Log("Manager2 connection to manager1 is ready (UseCaseDiscoveryCompleted).");

            await cemMangerReadyWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == controlBoxSki && status == DeviceConnectionStatus.UseCaseDiscoveryCompleted);
            Log("Manager1 is ready (UseCaseDiscoveryCompleted).");

            var data = new DeviceData
            {
                Lpc = new LpcLppData()
            };
            Log("Reading data from CEM...");
            await controlBoxManager.ReadDataAsync(data, cemSki);

            Assert.Equal(2000, data?.Lpc.Limit);
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
