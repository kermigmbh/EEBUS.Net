using EEBUS.Net;
using System.Net.Sockets;
using TestProject1.IntegrationTests;
using Xunit.Abstractions;

namespace TestProject1.Rpc
{
    public class EEBUSManagerRpcServerTests : EebusIntegrationTests
    {
        public EEBUSManagerRpcServerTests(ITestOutputHelper output) : base(output)
        {
        }

        private static int GetFreePort()
        {
            TcpListener probe = new(System.Net.IPAddress.Loopback, 0);
            probe.Start();
            int port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        [Fact]
        public async Task StartAndStopRpcServer_OpensAndReleasesPort()
        {
            using EEBUSManager manager = new(fileSystem: FileSystem, settings: Setup.GetCEMSettings(), logger: GetLogger("Manager"));
            int port = GetFreePort();

            await manager.StartRpcServerAsync(port);
            Assert.True(manager.IsRpcServerRunning);

            using (TcpClient client = new())
                await client.ConnectAsync("127.0.0.1", port);

            await manager.StopRpcServerAsync();
            Assert.False(manager.IsRpcServerRunning);

            await Assert.ThrowsAnyAsync<SocketException>(async () =>
            {
                using TcpClient client = new();
                await client.ConnectAsync("127.0.0.1", port);
            });
        }

        [Fact]
        public async Task StartRpcServer_WhenAlreadyRunning_ReplacesServerOnNewPort()
        {
            using EEBUSManager manager = new(fileSystem: FileSystem, settings: Setup.GetCEMSettings(), logger: GetLogger("Manager"));
            int oldPort = GetFreePort();
            int newPort = GetFreePort();
            await manager.StartRpcServerAsync(oldPort);

            await manager.StartRpcServerAsync(newPort);

            Assert.True(manager.IsRpcServerRunning);
            using (TcpClient client = new())
                await client.ConnectAsync("127.0.0.1", newPort);
            await Assert.ThrowsAnyAsync<SocketException>(async () =>
            {
                using TcpClient client = new();
                await client.ConnectAsync("127.0.0.1", oldPort);
            });
        }

        [Fact]
        public async Task StartRpcServer_WhenAlreadyRunningOnSamePort_Restarts()
        {
            using EEBUSManager manager = new(fileSystem: FileSystem, settings: Setup.GetCEMSettings(), logger: GetLogger("Manager"));
            int port = GetFreePort();
            await manager.StartRpcServerAsync(port);

            await manager.StartRpcServerAsync(port);

            Assert.True(manager.IsRpcServerRunning);
            using TcpClient client = new();
            await client.ConnectAsync("127.0.0.1", port);
        }

        [Fact]
        public async Task StartRpcServer_WhenPortIsInUse_ThrowsAndLeavesNoServerRunning()
        {
            using EEBUSManager manager = new(fileSystem: FileSystem, settings: Setup.GetCEMSettings(), logger: GetLogger("Manager"));
            TcpListener blocker = new(System.Net.IPAddress.Any, 0);
            blocker.Start();
            try
            {
                int busyPort = ((System.Net.IPEndPoint)blocker.LocalEndpoint).Port;
                await manager.StartRpcServerAsync(GetFreePort());

                await Assert.ThrowsAnyAsync<SocketException>(() => manager.StartRpcServerAsync(busyPort));

                Assert.False(manager.IsRpcServerRunning);
            }
            finally
            {
                blocker.Stop();
            }
        }

        [Fact]
        public async Task StopRpcServer_WhenNotRunning_DoesNothing()
        {
            using EEBUSManager manager = new(fileSystem: FileSystem, settings: Setup.GetCEMSettings(), logger: GetLogger("Manager"));

            await manager.StopRpcServerAsync();

            Assert.False(manager.IsRpcServerRunning);
        }

        [Fact]
        public async Task Dispose_StopsRpcServer()
        {
            int port = GetFreePort();
            EEBUSManager manager = new(fileSystem: FileSystem, settings: Setup.GetCEMSettings(), logger: GetLogger("Manager"));
            await manager.StartRpcServerAsync(port);

            manager.Dispose();

            await Assert.ThrowsAnyAsync<SocketException>(async () =>
            {
                using TcpClient client = new();
                await client.ConnectAsync("127.0.0.1", port);
            });
        }
    }
}
