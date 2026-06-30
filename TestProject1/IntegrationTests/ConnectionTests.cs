using EEBUS.Models;
using EEBUS.Net;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Xunit.Abstractions;

namespace TestProject1.IntegrationTests
{
    public class ConnectionTests : EebusIntegrationTests
    {
        public ConnectionTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task WHEN_ConnectionIsEstablished_THEN_ConnectionStatusIsUpdated()
        {
            ILogger manager1Logger = GetLogger("Manager1");
            ILogger manager2Logger = GetLogger("Manager2");
            using EEBUSManager manager1 = new EEBUSManager(Setup.GetCEMSettings(), logger: manager1Logger);
            using EEBUSManager manager2 = new EEBUSManager(Setup.GetControlBoxSettings(), logger: manager2Logger);

            string manager1Ski = manager1.GetLocalData().SKI;
            string manager2Ski = manager2.GetLocalData().SKI;
            manager1.AddTrustedSki(manager2Ski);
            manager2.AddTrustedSki(manager1Ski);

            DeviceConnectionStatus manager1ConnectionStatus = manager1.GetConnectionStatus(manager2Ski);
            DeviceConnectionStatus manager2ConnectionStatus = manager2.GetConnectionStatus(manager1Ski);

            Assert.Equal(DeviceConnectionStatus.Unknown, manager1ConnectionStatus);
            Assert.Equal(DeviceConnectionStatus.Unknown, manager2ConnectionStatus);

            await StartManagersAsync(manager1, manager2);

            using var manager2ReadyWaiter = new TestWaiter<RemoteDevice, DeviceConnectionStatus>(
                subscribe: handler => manager2.OnDeviceConnectionStatusChanged += handler,
                unsubscribe: handler => manager2.OnDeviceConnectionStatusChanged -= handler);
            using var manager1ReadyWaiter = new TestWaiter<RemoteDevice, DeviceConnectionStatus>(
                subscribe: handler => manager1.OnDeviceConnectionStatusChanged += handler,
                unsubscribe: handler => manager1.OnDeviceConnectionStatusChanged -= handler);

            bool connected = await manager1.TryConnectAsync(manager2Ski);
            Assert.True(connected);

            await manager2ReadyWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == manager1Ski);
            Log("Manager2 connection to manager1 is ready (UseCaseDiscoveryCompleted).");

            await manager1ReadyWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == manager2Ski);
            Log("Manager1 is ready (UseCaseDiscoveryCompleted).");
        }

        [Fact]
        public async Task WHEN_DisconnectHappens_THEN_ConnectionStatusIsUpdated()
        {
            ILogger manager1Logger = GetLogger("Manager1");
            ILogger manager2Logger = GetLogger("Manager2");
            using EEBUSManager manager1 = new EEBUSManager(Setup.GetCEMSettings(), logger: manager1Logger);
            using EEBUSManager manager2 = new EEBUSManager(Setup.GetControlBoxSettings(), logger: manager2Logger);

            string manager1Ski = manager1.GetLocalData().SKI;
            string manager2Ski = manager2.GetLocalData().SKI;
            manager1.AddTrustedSki(manager2Ski);
            manager2.AddTrustedSki(manager1Ski);

            await StartManagersAsync(manager1, manager2);

            bool connected = await manager1.TryConnectAsync(manager2Ski);
            Assert.True(connected);

            using var manager1ConnectionStatusWaiter = new TestWaiter<RemoteDevice, DeviceConnectionStatus>(
                subscribe: handler => manager1.OnDeviceConnectionStatusChanged += handler,
                unsubscribe: handler => manager1.OnDeviceConnectionStatusChanged -= handler);
            using var manager2ConnectionStatusWaiter = new TestWaiter<RemoteDevice, DeviceConnectionStatus>(
                subscribe: handler => manager2.OnDeviceConnectionStatusChanged += handler,
                unsubscribe: handler => manager2.OnDeviceConnectionStatusChanged -= handler);

            await manager1ConnectionStatusWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == manager2Ski && status == DeviceConnectionStatus.UseCaseDiscoveryCompleted);
            await manager2ConnectionStatusWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == manager1Ski && status == DeviceConnectionStatus.UseCaseDiscoveryCompleted);

            await manager1.DisconnectAsync(manager2Ski);
            await manager1ConnectionStatusWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == manager2Ski && status == DeviceConnectionStatus.Unknown);
            await manager2ConnectionStatusWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == manager1Ski && status == DeviceConnectionStatus.Unknown);
        }

        [Fact]
        public async Task WHEN_StopIsCalled_THEN_ConnectionStatusIsUpdated()
        {
            ILogger manager1Logger = GetLogger("Manager1");
            ILogger manager2Logger = GetLogger("Manager2");
            using EEBUSManager manager1 = new EEBUSManager(Setup.GetCEMSettings(), logger: manager1Logger);
            using EEBUSManager manager2 = new EEBUSManager(Setup.GetControlBoxSettings(), logger: manager2Logger);

            string manager1Ski = manager1.GetLocalData().SKI;
            string manager2Ski = manager2.GetLocalData().SKI;
            manager1.AddTrustedSki(manager2Ski);
            manager2.AddTrustedSki(manager1Ski);

            await StartManagersAsync(manager1, manager2);

            bool connected = await manager1.TryConnectAsync(manager2Ski);
            Assert.True(connected);

            using var manager1ConnectionStatusWaiter = new TestWaiter<RemoteDevice, DeviceConnectionStatus>(
               subscribe: handler => manager1.OnDeviceConnectionStatusChanged += handler,
               unsubscribe: handler => manager1.OnDeviceConnectionStatusChanged -= handler);
            using var manager2ConnectionStatusWaiter = new TestWaiter<RemoteDevice, DeviceConnectionStatus>(
                subscribe: handler => manager2.OnDeviceConnectionStatusChanged += handler,
                unsubscribe: handler => manager2.OnDeviceConnectionStatusChanged -= handler);

            await manager1ConnectionStatusWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == manager2Ski && status == DeviceConnectionStatus.UseCaseDiscoveryCompleted);
            await manager2ConnectionStatusWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == manager1Ski && status == DeviceConnectionStatus.UseCaseDiscoveryCompleted);

            await Task.Delay(2000);

            await manager2.StopAsync();

            await manager1ConnectionStatusWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == manager2Ski && status == DeviceConnectionStatus.Unknown, 15000);
            await manager2ConnectionStatusWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == manager1Ski && status == DeviceConnectionStatus.Unknown, 15000);
        }
    }
}
