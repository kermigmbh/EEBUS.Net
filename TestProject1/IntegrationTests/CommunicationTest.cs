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
    public class CommunicationTest
    {
        private readonly ITestOutputHelper _output;

        public CommunicationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private void Log(string message)
        {
            Debug.WriteLine(message);
            _output.WriteLine(message);
            
        }


        [Fact]
        public async Task TwoEEBUSDevicesConnectivityAsync()
        {
            ILogger manager1Logger = new TestOutputLogger(_output, "Manager1");
            ILogger manager2Logger = new TestOutputLogger(_output, "Manager2");
            EEBUSManager manager1 = new EEBUSManager(Setup.GetCEMSettings(), logger: manager1Logger);
            EEBUSManager manager2 = new EEBUSManager(Setup.GetControlBoxSettings(), logger: manager2Logger);

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

            manager1.AddTrustedSki(manager2.GetLocalData().SKI);
            manager2.AddTrustedSki(manager1.GetLocalData().SKI);

            string manager2Ski = manager2.GetLocalData().SKI;
            string manager1Ski = manager1.GetLocalData().SKI;

            using var manager2FoundWaiter = new TestWaiter<RemoteDevice>(
                subscribe: handler => manager1.OnDeviceFound += handler,
                unsubscribe: handler => manager1.OnDeviceFound -= handler);

            using var manager1FoundWaiter = new TestWaiter<RemoteDevice>(
               subscribe: handler => manager2.OnDeviceFound += handler,
               unsubscribe: handler => manager2.OnDeviceFound -= handler);

            manager1.Start();
            manager2.Start();

            var t1 = manager2FoundWaiter.Match(device => device.SKI.ToString() == manager2Ski, 15000);
            var t2 =  manager1FoundWaiter.Match(device => device.SKI.ToString() == manager1Ski, 15000);
            await Task.WhenAll(t1, t2);
            Log("Manager1 found manager2.");

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
    }
}
