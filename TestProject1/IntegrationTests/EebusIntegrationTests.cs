using EEBUS.Models;
using EEBUS.Net;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit.Abstractions;

namespace TestProject1.IntegrationTests
{
    public class EebusIntegrationTests
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger _defaultLogger;

        public EebusIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _defaultLogger = GetLogger("Log");
        }

        protected void Log(string message, [CallerMemberName] string testName = "")
        {
            Debug.WriteLine($"[{testName}] {message}");
            //_output.WriteLine(message);
            _defaultLogger.LogTrace($"[{testName}] {message}");
        }

        protected ILogger GetLogger(string categoryName, [CallerMemberName] string testName = "")
        {
            return new TestOutputLogger(_output, $"[{testName}] {categoryName}");
        }

        protected async Task StartManagersAsync(EEBUSManager manager1, EEBUSManager manager2)
        {
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


            using var manager2FoundWaiter = new TestWaiter<RemoteDevice>(
             subscribe: handler => manager1.OnDeviceFound += handler,
             unsubscribe: handler => manager1.OnDeviceFound -= handler);
            using var manager1FoundWaiter = new TestWaiter<RemoteDevice>(
               subscribe: handler => manager2.OnDeviceFound += handler,
               unsubscribe: handler => manager2.OnDeviceFound -= handler);
            manager1.Start();
            manager2.Start();
            var t1 = manager2FoundWaiter.Match(device => device.SKI.ToString() == manager2.GetLocalData().SKI, 15000);
            var t2 = manager1FoundWaiter.Match(device => device.SKI.ToString() == manager1.GetLocalData().SKI, 15000);
            await Task.WhenAll(t1, t2);
            Log("Manager1 found manager2.");
        }
    }
}
