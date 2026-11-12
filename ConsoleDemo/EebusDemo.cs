using EEBUS;
using EEBUS.Models;
using EEBUS.Net;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.Net.Events;
using EEBUS.Net.Rpc;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Net.Security;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ConsoleDemo
{
    public class EebusDemo
    {
        private EEBUSManager? _manager;
        private readonly RpcServer _rpcServer = new(new DemoRpcHandler(), new RpcConsoleLogger());

        public async Task RunAsync(Settings settings)
        {

            var onNewConnectionValidation = (NewConnectionValidationEventArgs args) =>
            {
                Debug.WriteLine("Ski: " + args.Ski);
                Debug.WriteLine("EP: " + args.RemoteEndpoint);
                return true;
            };

            _manager = new EEBUSManager(settings, fileSystem: new FileSystem());




            _manager.OnLimitDataChanged = async (args) =>
            {
                Console.WriteLine($"Limit data changed: {args}");
            };




            _manager.Start();

            var localDevice = _manager.GetLocalData();
            string ski = localDevice.SKI.ToString();
            Console.WriteLine("Local SKI: " + ski + "\n");
            Console.WriteLine("Supported Commands:");
            Console.WriteLine("- remotes: prints all remote devices that were found through mdns");
            Console.WriteLine("- remote <index>: prints all data of the remote device on index <index>");
            Console.WriteLine("- connect <index>: connects to the remote device with index <index>, starting at 0");
            Console.WriteLine("- read: prints out all properties of the local device");
            Console.WriteLine("- ski: sets trusted ski");
            Console.WriteLine("- visualize <message>: analyzes a ship message and prints the structure of a nodeManagementDetailedDiscoveryData or nodeManagementUseCaseData payload");
            Console.WriteLine("- rpc start <port>: starts the JSON-RPC server on <port> (restarts it if already running) and logs RPC traffic");
            Console.WriteLine("- rpc stop: stops the JSON-RPC server");


            while (true)
            {
                Console.Write(":> ");
                string? input = Console.ReadLine();
                if (input == null) continue;
                if (input == "exit") break;

                string[] tokens = input.Split(null);

                switch (tokens[0])
                {
                    case "remotes":
                        PrintRemotes();
                        break;
                    case "ski":
                        string trustedSki = tokens.ElementAtOrDefault(1);
                        _manager.AddTrustedSki(trustedSki);
                        break;
                    case "remote":
                        if (int.TryParse(tokens.ElementAtOrDefault(1), out int remoteIndex))
                        {
                            PrintRemoteData(remoteIndex);
                        }
                        else
                        {
                            Console.WriteLine("Invalid input!");
                        }
                        break;
                    case "connect":
                        if (int.TryParse(tokens.ElementAtOrDefault(1), out int connectIndex))
                        {
                            bool connected = await ConnectAsync(connectIndex);
                            if (connected)
                            {
                                Console.WriteLine("Client connected!");
                            }
                            else
                            {
                                Console.WriteLine("Failed to connect client!");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid input!");
                        }
                        break;
                    case "read":
                        Read();
                        break;
                    case "visualize":
                        MessageVisualizer.Visualize(input.Substring(tokens[0].Length));
                        break;
                    case "rpc":
                        await HandleRpcCommandAsync(tokens);
                        break;
                    default:
                        Console.WriteLine("Invalid input: valid commands: remotes; connect <index>; read; visualize <message>; rpc start <port>; rpc stop");
                        break;
                }
            }
            await _rpcServer.StopAsync();
            _manager.Dispose();
        }

        private async Task HandleRpcCommandAsync(string[] tokens)
        {
            switch (tokens.ElementAtOrDefault(1))
            {
                case "start":
                    if (!int.TryParse(tokens.ElementAtOrDefault(2), out int port))
                    {
                        Console.WriteLine("Usage: rpc start <port>");
                        return;
                    }

                    try
                    {
                        await _rpcServer.StartAsync(port);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to start RPC server on port {port}: {ex.Message}");
                    }
                    break;
                case "stop":
                    if (!_rpcServer.IsRunning)
                    {
                        Console.WriteLine("RPC server is not running.");
                        return;
                    }

                    await _rpcServer.StopAsync();
                    break;
                default:
                    Console.WriteLine("Usage: rpc start <port> | rpc stop");
                    break;
            }
        }

        private void Read()
        {
            var local = _manager?.GetLocalData();
            if (local == null)
            {
                Console.WriteLine("No local device found");
                return;
            }

            Console.WriteLine(local?.ToString());
        }

        private void PrintRemotes()
        {
            var remotes = _manager?.GetDiscoveredDevices();
            if (remotes != null)
            {
                foreach (var remote in remotes)
                {
                    Console.WriteLine(remote?.ToString());
                }
            }
        }

        private void PrintRemoteData(int index)
        {
            IEnumerable<RemoteDeviceData>? remotes = _manager?.GetDiscoveredDevices();
            RemoteDeviceData? match = remotes?.ElementAtOrDefault(index);
            if (match == null) return;
            DeviceData? remoteData = _manager?.GetRemoteData(match.Ski);

            if (remoteData != null)
            {
                Console.WriteLine(JsonSerializer.Serialize<DeviceData>(remoteData));
            }
        }

        private async Task<bool> ConnectAsync(int remoteIndex)
        {
            if (_manager == null) return false;

            var remotes = _manager.GetDiscoveredDevices();
            if (remotes == null) return false;

            var remote = remotes.ElementAtOrDefault(remoteIndex);
            if (remote == null) return false;

             

            try
            {
                return await _manager.TryConnectAsync(remote.Ski);
            }
            catch (Exception ex)
            {
                return false;
            }

        }
    }
}
