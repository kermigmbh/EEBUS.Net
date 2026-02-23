using EEBUS.Models;
using EEBUS.Net.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EEBUS
{
    public class SHIPListener
    {

        private Devices devices;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private WebApplication? _app;
        public event EventHandler<DeviceConnectionChangedEventArgs>? OnDeviceConnectionChanged;
        private Settings _settings;

        public Func<NewConnectionValidationEventArgs, bool>? OnNewConnectionValidation { get; set; } = (NewConnectionValidationEventArgs args) => true;

        public SHIPListener(Devices devices, Settings settings)
        {
            this.devices = devices;
            _settings = settings;
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

        private bool CertificateCallback(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors sslPolicyErrors, IPEndPoint remoteEndpoint)
        {
            if (cert == null)
            {
                return false;
            }
            Console.WriteLine(remoteEndpoint.ToString());

            byte[] hash = SHA1.Create().ComputeHash(cert.GetPublicKey() ?? []);
            var ski = new SKI(hash);
            var skiString = ski.ToString();

            return OnNewConnectionValidation?.Invoke(new NewConnectionValidationEventArgs()
            {
                Certificate = new X509Certificate2(cert),
                RemoteEndpoint = remoteEndpoint.ToString(),
                Ski = skiString

            }) ?? false;
        }

        private async Task StartStandaloneInternalAsync(int port, CancellationToken cancellationToken)
        {
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
            builder.WebHost.UseKestrel();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ConfigureHttpsDefaults(httpOptions =>
                {
                    httpOptions.ServerCertificate = CertificateGenerator.GenerateCert(_settings.BasePath, _settings.Certificate);

                    //commented, because the settings are in OnAuthenticate. It will not work if both are set!!!
                    //httpOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                    //httpOptions.ClientCertificateValidation = ClientCertificateValidation;

                    httpOptions.SslProtocols = SslProtocols.Tls12;
                    httpOptions.OnAuthenticate = (connectionContext, authenticationOptions) =>
                    {

                        authenticationOptions.EnabledSslProtocols = SslProtocols.Tls12;
                        // Get remote endpoint (IP:port) from socket feature
                        //var socketFeature = connectionContext?.Features.Get<IConnectionSocketFeature>();
                        //var remoteEndPoint = socketFeature?.Socket.RemoteEndPoint as IPEndPoint;
                        //var remoteIp = remoteEndPoint?.Address;
                        authenticationOptions.ClientCertificateRequired = true;
                        authenticationOptions.ServerCertificate = CertificateGenerator.GenerateCert(_settings.BasePath, _settings.Certificate);
                        authenticationOptions.RemoteCertificateValidationCallback =
                            (sender, cert, chain, sslPolicyErrors) =>
                            {
                                
                                return CertificateCallback(sender, cert, chain, sslPolicyErrors, connectionContext?.RemoteEndPoint as IPEndPoint ?? throw new Exception("not a ip endpoint: " + connectionContext?.RemoteEndPoint?.ToString()));
                            };
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
                Server? server = null;
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

                    server = Server.Get(httpContext.Request.Host);
                    if (server != null)
                    {
                        Debug.WriteLine("Middleware Weiterleitung, Server vorhanden und stoppen");
                        await server.CloseAsync().ConfigureAwait(false);
                        OnDeviceConnectionChanged?.Invoke(this, new DeviceConnectionChangedEventArgs() { Connection = server, ChangeType = DeviceConnectionChangeType.Disconnected });
                    }

                    var socket = await httpContext.WebSockets.AcceptWebSocketAsync("ship").ConfigureAwait(false);
                    if (socket == null || socket.State != WebSocketState.Open)
                    {
                        Console.WriteLine("Failed to accept socket from " + httpContext.Request.Host.ToUriComponent());
                        return;
                    }

                    server = new Server(httpContext.Request.Host, socket, this.devices);
                    OnDeviceConnectionChanged?.Invoke(this, new DeviceConnectionChangedEventArgs() { Connection = server, ChangeType = DeviceConnectionChangeType.Connected });
                    await server.Do().ConfigureAwait(false);

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: " + ex.Message);

                    httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await httpContext.Response.WriteAsync("Error while processing websocket: " + ex.Message).ConfigureAwait(false);
                }
                finally
                {
                    if (server != null)
                    {
                        OnDeviceConnectionChanged?.Invoke(this, new DeviceConnectionChangedEventArgs() { Connection = server, ChangeType = DeviceConnectionChangeType.Disconnected });
                    }

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
