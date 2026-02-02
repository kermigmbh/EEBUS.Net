using EEBUS;
using EEBUS.Models;
using EEBUS.Net;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleDemo
{
    public class EebusDemo
    {
        private EEBUSManager? _manager;

        private List<string> s_connectedClients = [];

        public async Task RunAsync(Settings settings)
        {
            _manager = new EEBUSManager(settings);

            _manager.StartServer();
            _manager.StartDeviceSearch();

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
                    case "connect":
                        if (int.TryParse(tokens.ElementAtOrDefault(1), out int remoteIndex))
                        {
                            bool connected = await ConnectAsync(remoteIndex);
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
                        Read(tokens.ElementAtOrDefault(1) ?? string.Empty);
                        break;
                    default:
                        Console.WriteLine("Invalid input!");
                        break;
                }
            }

            _manager.StopDeviceSearch();

            foreach (string hostString in s_connectedClients)
            {
                await _manager.DisconnectAsync(new Microsoft.AspNetCore.Http.HostString(hostString));
            }
            _manager.Dispose();
        }

        private void Read(string address)
        {
            var local = _manager?.GetLocal();
            if (local == null)
            {
                Console.WriteLine("No local device found");
                return;
            }

            if (local.TryGetValue(address, StringComparison.OrdinalIgnoreCase, out JToken? value))
            {
                Console.WriteLine($"{address}: {value.ToString()}");
            }
        }

        private void PrintRemotes()
        {
            JArray? remotes = _manager?.GetRemotes();
            if (remotes != null)
            {
                foreach (JToken remote in remotes)
                {
                    Console.WriteLine(remote.ToString());
                }
            }
        }

        private async Task<bool> ConnectAsync(int remoteIndex)
        {
            if (_manager == null) return false;

            JArray? remotes = _manager.GetRemotes();
            if (remotes == null) return false;

            JToken? remote = remotes.ElementAtOrDefault(remoteIndex);
            if (remote == null) return false;

            string? ski = remote.Value<string>("ski");
            if (ski == null) return false;

            string? hostString = await _manager.ConnectAsync(ski);
            if (hostString == null) return false;

            s_connectedClients.Add(hostString);
            return true;
        }
    }
}
