using EEBUS;
using EEBUS.Models;
using EEBUS.Net;
using EEBUS.Net.Events;
using System.Diagnostics;
using System.Net.Security;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ConsoleDemo
{
    public class EebusDemo
    {
        private EEBUSManager? _manager;

        private List<string> s_connectedClients = [];

        public async Task RunAsync(Settings settings)
        {
            var onNewConnectionValidation = (NewConnectionValidationEventArgs args) =>
            {
                Debug.WriteLine("Ski: " + args.Ski);
                Debug.WriteLine("EP: " + args.RemoteEndpoint);
                return true;
            };

            _manager = new EEBUSManager(settings, onNewConnectionValidation);

            


            _manager.OnLimitDataChanged = async (args) =>
            {
                Console.WriteLine($"Limit data changed: {args}");
            };




            _manager.Start();

            JsonObject localDevice = _manager.GetLocal();
            Console.WriteLine("[EEBUS Demo]");
            localDevice.TryGetPropertyValue("ski", out JsonNode? jsonNode);
            string ski = string.Empty;
            if (jsonNode != null)
            {
                ski = Regex.Replace(jsonNode.ToString(), @"\s+", "");   //remove whitespaces
            }
            Console.WriteLine("Local SKI: " + ski + "\n");
            Console.WriteLine("Supported Commands:");
            Console.WriteLine("- remotes: prints all remote devices that were found through mdns");
            Console.WriteLine("- connect <index>: connects to the remote device with index <index>, starting at 0");
            Console.WriteLine("- read: prints out all properties of the local device");


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
                        Read();
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

        private void Read()
        {
            var local = _manager?.GetLocal();
            if (local == null)
            {
                Console.WriteLine("No local device found");
                return;
            }

            Console.WriteLine(local?.ToString());
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
