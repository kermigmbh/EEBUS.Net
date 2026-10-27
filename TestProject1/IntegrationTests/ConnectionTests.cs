using EEBUS;
using EEBUS.Models;
using EEBUS.Net;
using EEBUS.SHIP.Messages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
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

        [Fact]
        public async Task WHEN_DisconnectHappens_THEN_CloseAnnounceIsConfirmedByPeer()
        {
            ILogger manager1Logger = GetLogger("Manager1");
            ILogger manager2Logger = GetLogger("Manager2");
            using EEBUSManager manager1 = new EEBUSManager(Setup.GetCEMSettings(withPeriodicReconnect: false), logger: manager1Logger);
            using EEBUSManager manager2 = new EEBUSManager(Setup.GetControlBoxSettings(withPeriodicReconnect: false), logger: manager2Logger);

            string manager1Ski = manager1.GetLocalData().SKI;
            string manager2Ski = manager2.GetLocalData().SKI;

            List<CloseMessage> sentByManager1 = [];
            List<CloseMessage> sentByManager2 = [];
            manager1.BeforeMessageSent = (message, connection) =>
            {
                if (message is CloseMessage close) lock (sentByManager1) sentByManager1.Add(close);
                return Task.FromResult(true);
            };
            manager2.BeforeMessageSent = (message, connection) =>
            {
                if (message is CloseMessage close) lock (sentByManager2) sentByManager2.Add(close);
                return Task.FromResult(true);
            };

            await StartAndConnectManagersAsync(manager1, manager2);

            using var manager2ConnectionStatusWaiter = new TestWaiter<RemoteDevice, DeviceConnectionStatus>(
                subscribe: handler => manager2.OnDeviceConnectionStatusChanged += handler,
                unsubscribe: handler => manager2.OnDeviceConnectionStatusChanged -= handler);

            await manager1.DisconnectAsync(manager2Ski, ConnectionCloseReasonType.removedConnection);

            await manager2ConnectionStatusWaiter.Match((remoteDevice, status) => remoteDevice.SKI.ToString() == manager1Ski && status == DeviceConnectionStatus.Unknown);

            CloseMessage announce = Assert.Single(sentByManager1);
            Assert.Equal(ConnectionClosePhaseType.announce, announce.connectionClose.phase);
            Assert.Equal(ConnectionCloseReasonType.removedConnection, announce.connectionClose.reason);
            Assert.Equal(CloseMessage.DefaultMaxTimeMs, announce.connectionClose.maxTime);

            CloseMessage confirm = Assert.Single(sentByManager2);
            Assert.Equal(ConnectionClosePhaseType.confirm, confirm.connectionClose.phase);
            Assert.Null(confirm.connectionClose.maxTime);
            Assert.Null(confirm.connectionClose.reason);

            Assert.Null(manager2.GetConnection(manager1Ski));
            Assert.Null(manager1.GetConnection(manager2Ski));
        }

        [Fact]
        public async Task WHEN_DoubleConnectionIsOpened_THEN_SecondConnectAttemptWithoutOverrideIsRejected()
        {
            ILogger manager1Logger = GetLogger("Manager1");
            ILogger manager2Logger = GetLogger("Manager2");
            using EEBUSManager manager1 = new EEBUSManager(Setup.GetCEMSettings(withPeriodicReconnect: false), logger: manager1Logger);
            using EEBUSManager manager2 = new EEBUSManager(Setup.GetControlBoxSettings(withPeriodicReconnect: false), logger: manager2Logger);

            string manager1Ski = manager1.GetLocalData().SKI;
            string manager2Ski = manager2.GetLocalData().SKI;

            await StartAndConnectManagersAsync(manager1, manager2);
            Connection? existing = manager1.GetConnection(manager2Ski);

            bool connected = await manager1.TryConnectAsync(manager2Ski);

            Assert.False(connected);
            Assert.Same(existing, manager1.GetConnection(manager2Ski));
            Assert.Equal(WebSocketState.Open, existing!.WebSocket.State);
        }
    }
}
