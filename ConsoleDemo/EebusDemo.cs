using EEBUS;
using EEBUS.Models;
using EEBUS.Net;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;

namespace ConsoleDemo
{
    public class EebusDemo
    {
        private EEBUSManager? _manager;

        private List<string> s_connectedClients = [];

        public async Task RunAsync(Settings settings)
        {
            _manager = new EEBUSManager(settings);
            _manager.Start();

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
                        Console.WriteLine("Invalid input: valid commands: remotes; connect <index>; read");
                        break;
                }
            }

            _manager.Stop();

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

            if (local.TryGetPropertyValue(address, out var value))
            {
                Console.WriteLine($"{address}: {value.ToString()}");
            }
        }

        private void PrintRemotes()
        {
            JsonArray? remotes = _manager?.GetRemotes();
            if (remotes != null)
            {
                foreach (var remote in remotes)
                {
                    Console.WriteLine(remote?.ToString());
                }
            }
        }

        private async Task<bool> ConnectAsync(int remoteIndex)
        {
            if (_manager == null) return false;

            JsonArray? remotes = _manager.GetRemotes();
            if (remotes == null) return false;

            var remote = remotes.ElementAtOrDefault(remoteIndex);
            if (remote == null) return false;

            var ski = remote["ski"];
            if (ski == null) return false;

            string? hostString = await _manager.ConnectAsync(ski.ToString());
            if (hostString == null) return false;

            s_connectedClients.Add(hostString);
            return true;
        }
    }
}
