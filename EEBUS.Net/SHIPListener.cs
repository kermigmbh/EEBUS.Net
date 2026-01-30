using EEBUS.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Net.Security;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace EEBUS
{
    public class SHIPListener
    {

        private Devices devices;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private WebApplication? _app;

        public SHIPListener(Devices devices)
        {
            this.devices = devices;
        }
      
        public Task StartAsync(int port)
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            return Task.Run(async () =>
            {
                try
                {
                    //var listener = new SHIPListener(devices);
                    await StartStandaloneInternalAsync(port, _cts.Token);
                }
                catch (Exception ex)
                {
                    await StopAsync();
                    Console.WriteLine("Exception in SHIPListener: " + ex.ToString());
                    throw;
                }
            });
        }
        public async Task StopAsync()
        {
            _cts.Cancel();
            if (_app != null)
            {
                await _app.StopAsync();
            }
        }


        private async Task StartStandaloneInternalAsync(int port, CancellationToken cancellationToken)
        {
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
            builder.WebHost.UseKestrel();
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

            });
            //builder.Services.AddCors(options =>
            //{
            //    options.AddPolicy("AllowAll", builder =>
            //        builder.AllowAnyOrigin()
            //               .AllowAnyMethod()
            //               .AllowAnyHeader());
            //});
            var app = builder.Build();
            _app = app;


            // Enable WebSockets (you can tweak KeepAliveInterval, etc.)
            app.UseWebSockets();

            // Handle the /ws endpoint
            //app.Map("/{*path}", async httpContext =>
            app.Run(async httpContext =>
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

            app.Urls.Add($"https://0.0.0.0:{port}");
            //app.UseCors("AllowAll");
            await app.RunAsync(cancellationToken);

           

        }
        private bool ProtocolSupported(HttpContext httpContext)
        {
            IList<string> requestedProtocols = httpContext.WebSockets.WebSocketRequestedProtocols;
            return (0 < requestedProtocols.Count) && requestedProtocols.Contains("ship");
        }



    }
}
