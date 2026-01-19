using EEBUS.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

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

                    await listener.StartStandaloneInternalAsync(port);
                }
                catch (Exception ex)
                {
                    Close();
                    Console.WriteLine("Exception in SHIPListener: " + ex.ToString());
                    throw;
                }

            });
        }

        private async Task StartStandaloneInternalAsync(int port)
        {

            var options = new WebApplicationOptions
            {
                Args = new[] { "--hostingStartupAssemblies", "" }
            };
            var builder = WebApplication.CreateBuilder(options);


            
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ConfigureHttpsDefaults(httpOptions =>
                {
                    httpOptions.ServerCertificate = CertificateGenerator.GenerateCert("EEBUS.net");
                    httpOptions.ClientCertificateMode = ClientCertificateMode.NoCertificate;
                    httpOptions.ClientCertificateValidation = (X509Certificate2 certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) => true;
                    httpOptions.SslProtocols = SslProtocols.Tls12;
                    httpOptions.OnAuthenticate = (connectionContext, authenticationOptions) =>
                    {
                        authenticationOptions.EnabledSslProtocols = SslProtocols.Tls12;
                    };
                });
                //options.ListenAnyIP(port, listenOptions =>
                //{
                //    listenOptions.UseHttps("cert.pfx", "mypassword");
                //});
            });
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", builder =>
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader());
            });
            var app = builder.Build();

            // Enable WebSockets (you can tweak KeepAliveInterval, etc.)
            app.UseWebSockets();
           
            // Handle the /ws endpoint
            app.Map("/{*path}", async httpContext =>
            {
                try
                {
                    if (!httpContext.WebSockets.IsWebSocketRequest)
                    {
                        Debug.WriteLine("Middleware Weiterleitung, kein WebSocket Request");
                        // passed on to next middleware
                        //await next(httpContext).ConfigureAwait(false);
                        return;
                    }

                    if (!ProtocolSupported(httpContext))
                    {
                        Debug.WriteLine("Middleware Weiterleitung, kein ship Request");
                        httpContext.Response.StatusCode = 400;
                        
                        // passed on to next middleware
                        //await this.next(httpContext).ConfigureAwait(false);
                        return;
                    }

                    Server server = Server.Get(httpContext.Request.Host);
                    if (server != null)
                    {
                        Debug.WriteLine("Middleware Weiterleitung, Server vorhanden und stoppen");
                        await server.Close().ConfigureAwait(false);
                    }

                    var socket = await httpContext.WebSockets.AcceptWebSocketAsync("ship").ConfigureAwait(false);
                    if (socket == null || socket.State != WebSocketState.Open)
                    {
                        Console.WriteLine("Failed to accept socket from " + httpContext.Request.Host.ToUriComponent());
                        return;
                    }

                    server = new Server(httpContext.Request.Host, socket, this.devices);
                    await server.Do().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: " + ex.Message);

                    httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await httpContext.Response.WriteAsync("Error while processing websocket: " + ex.Message).ConfigureAwait(false);
                }
            });

            // Listen on all interfaces port 8080 (HTTP)
            app.Urls.Add($"https://0.0.0.0:{port}");
            app.UseCors("AllowAll");
            await app.RunAsync();

        }
        private bool ProtocolSupported(HttpContext httpContext)
        {
            IList<string> requestedProtocols = httpContext.WebSockets.WebSocketRequestedProtocols;
            return (0 < requestedProtocols.Count) && requestedProtocols.Contains("ship");
        }

        //private async Task StartStandaloneInternal(int port)
        //{
        //    _listener = new HttpListener();
        //    _listener.Prefixes.Add($"https://*:{port}/ws/");
        //    _listener.Start();

        //    Console.WriteLine("WebSocket server running ");

        //    while (!_cts.IsCancellationRequested)
        //    {
        //        var context = await _listener.GetContextAsync();

        //        if (context.Request.IsWebSocketRequest)
        //        {
        //            if (!ProtocolSupported(context))
        //            {
        //                context.Response.StatusCode = 400;
        //                context.Response.Close();
        //                continue;
        //            }

        //            var hostHeader = new HostString(context.Request.Url.Host, context.Request.Url.Port);

        //            Server server = Server.Get(hostHeader);
        //            if (server != null)
        //            {
        //                Debug.WriteLine("Middleware Weiterleitung, Server vorhanden und stoppen");
        //                await server.Close().ConfigureAwait(false);
        //            }

        //            var wsContext = await context.AcceptWebSocketAsync("ship");
        //            var socket = wsContext.WebSocket;
        //            if (socket == null || socket.State != WebSocketState.Open)
        //            {
        //                Console.WriteLine("Failed to accept socket from " + hostHeader.ToUriComponent());
        //                return;
        //            }

        //            server = new Server(hostHeader, socket, this.devices);
        //            await server.Do().ConfigureAwait(false);

        //        }
        //        else
        //        {
        //            context.Response.StatusCode = 400;
        //            context.Response.Close();
        //        }
        //    }

        //}
        //private static bool ProtocolSupported(HttpListenerContext context)
        //{
        //    string? header = context.Request.Headers["Sec-WebSocket-Protocol"];

        //    if (string.IsNullOrWhiteSpace(header))
        //        return false;

        //    // Split comma-separated list
        //    var requestedProtocols = header
        //        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        //    return requestedProtocols.Contains("ship", StringComparer.OrdinalIgnoreCase);
        //}

    }
}
