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
    public class ConnectionTests
    {
        private readonly ITestOutputHelper _output;

        public ConnectionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private void Log(string message)
        {
            Debug.WriteLine(message);
            _output.WriteLine(message);

        }


        /*
         *  using var manager2ReadyWaiter = new TestWaiter<RemoteDevice, DeviceConnectionStatus>(
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
         */

        [Fact]
        public async Task WHEN_ConnectionIsEstablished_THEN_ConnectionStatusIsUpdated()
        {
            ILogger manager1Logger = new TestOutputLogger(_output, "Manager1");
            ILogger manager2Logger = new TestOutputLogger(_output, "Manager2");
            EEBUSManager manager1 = new EEBUSManager(Setup.GetCEMSettings(), logger: manager1Logger);
            EEBUSManager manager2 = new EEBUSManager(Setup.GetControlBoxSettings(), logger: manager2Logger);

            string manager1Ski = manager1.GetLocalData().SKI;
            string manager2Ski = manager2.GetLocalData().SKI;
            manager1.AddTrustedSki(manager2Ski);
            manager2.AddTrustedSki(manager1Ski);

            DeviceConnectionStatus manager1ConnectionStatus = manager1.GetConnectionStatus(manager2Ski);
            DeviceConnectionStatus manager2ConnectionStatus = manager2.GetConnectionStatus(manager1Ski);

            Assert.Equal(DeviceConnectionStatus.Unknown, manager1ConnectionStatus);
            Assert.Equal(DeviceConnectionStatus.Unknown, manager2ConnectionStatus);

            manager1.OnDeviceConnectionStatusChanged = (device, status) =>
            {
                Log($"Manager1: Device {device.SKI} connection status changed to {status}");
                manager1ConnectionStatus = status;
                return Task.CompletedTask;
            };
            manager2.OnDeviceConnectionStatusChanged = (device, status) =>
            {
                Log($"Manager2: Device {device.SKI} connection status changed to {status}");
                manager2ConnectionStatus = status;
                return Task.CompletedTask;
            };

          

            bool connected = await manager1.TryConnectAsync(manager2Ski);
            Assert.True(connected);

            for (int i = 0; i < 5; i++)
            {
                Log($"Manager1 connection status: {manager1ConnectionStatus}");
                Log($"Manager2 connection status: {manager2ConnectionStatus}");
                if (manager1ConnectionStatus == DeviceConnectionStatus.UseCaseDiscoveryCompleted &&
                    manager2ConnectionStatus == DeviceConnectionStatus.UseCaseDiscoveryCompleted)
                {
                    break;
                }
                await Task.Delay(3000);
            }

            Assert.Equal(DeviceConnectionStatus.UseCaseDiscoveryCompleted, manager1ConnectionStatus);
            Assert.Equal(DeviceConnectionStatus.UseCaseDiscoveryCompleted, manager2ConnectionStatus);
        }
    }
}
