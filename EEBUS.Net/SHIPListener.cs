using EEBUS.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace EEBUS
{
    public class SHIPListener
    {

        private Devices devices;

        public SHIPListener(Devices devices)
        {

            this.devices = devices;
        }

       private CancellationTokenSource _cts = new CancellationTokenSource();
        private HttpListener _listener;

        public void Close()
        {
            _listener.Close();
            _cts.Cancel();
        }
        public Task StartStandaloneAsync(int port)
        {
            return Task.Run(async () =>

            {
                try
                {

                    var listener = new SHIPListener(devices);

                    await listener.StartStandaloneInternal(port);
                }
                catch (Exception ex)
                {
                    Close();
                    Console.WriteLine("Exception in SHIPListener: " + ex.ToString());
                    throw;
                }

            });
        }
        private async Task StartStandaloneInternal(int port)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"https://localhost:{port}/ws/");
            _listener.Start();

            Console.WriteLine("WebSocket server running ");

            while (!_cts.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    if (!ProtocolSupported(context))
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                        continue;
                    }

                    var hostHeader = new HostString(context.Request.Url.Host, context.Request.Url.Port);
                    
                    Server server = Server.Get(hostHeader);
                    if (server != null)
                    {
                        Debug.WriteLine("Middleware Weiterleitung, Server vorhanden und stoppen");
                        await server.Close().ConfigureAwait(false);
                    }

                    var wsContext = await context.AcceptWebSocketAsync("ship");
                    var socket = wsContext.WebSocket;
                    if (socket == null || socket.State != WebSocketState.Open)
                    {
                        Console.WriteLine("Failed to accept socket from " + hostHeader.ToUriComponent());
                        return;
                    }

                    server = new Server(hostHeader, socket, this.devices);
                    await server.Do().ConfigureAwait(false);

                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }

        }
        private static bool ProtocolSupported(HttpListenerContext context)
        {
            string? header = context.Request.Headers["Sec-WebSocket-Protocol"];

            if (string.IsNullOrWhiteSpace(header))
                return false;

            // Split comma-separated list
            var requestedProtocols = header
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return requestedProtocols.Contains("ship", StringComparer.OrdinalIgnoreCase);
        }

    }
}
