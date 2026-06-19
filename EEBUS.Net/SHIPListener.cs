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
using Microsoft.Extensions.Logging;
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
        private X509Certificate2? _serverCertificate;
        //public event EventHandler<DeviceConnectionChangedEventArgs>? OnDeviceConnectionChanged;
        private ILogger? _logger;

        public Func<DeviceConnectionChangedEventArgs, Task>? OnDeviceConnectionChanged;

        private Settings _settings;

        public Func<NewConnectionValidationEventArgs, bool>? OnNewConnectionValidation { get; set; } = (NewConnectionValidationEventArgs args) => true;

        public SHIPListener(Devices devices, Settings settings, ILogger? logger = null)
        {
            this.devices = devices;
            _settings = settings;
            _logger = logger;
        }

        public Task StartAsync(int port)
        {
            var previousCts = _cts;
            _cts = new CancellationTokenSource();
            try
            {
                previousCts.Cancel();
            }
            catch (ObjectDisposedException) { }
            finally
            {
                try { previousCts.Dispose(); } catch { }
            }

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
                try
                {
                    await _app.StopAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("SHIPListener.StopAsync: app stop failed: " + ex.Message);
                }

                try
                {
                    await _app.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("SHIPListener.StopAsync: app dispose failed: " + ex.Message);
                }

                _app = null;
            }

            // Only safe to dispose AFTER Kestrel has fully drained; in-flight
            // handshakes may otherwise touch a disposed certificate.
            if (_serverCertificate != null)
            {
                try { _serverCertificate.Dispose(); } catch { }
                _serverCertificate = null;
            }

            try { _cts.Dispose(); } catch { }
        }

        private bool CertificateCallback(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors sslPolicyErrors, IPEndPoint remoteEndpoint)
        {
            if (cert == null)
            {
                return false;
            }
           // Console.WriteLine(remoteEndpoint.ToString());

            byte[] hash = SHA1.HashData(cert.GetPublicKey() ?? []);
            var ski = new SKI(hash);
            var skiString = ski.ToString();

            var result =  OnNewConnectionValidation?.Invoke(new NewConnectionValidationEventArgs()
            {
                Certificate = new X509Certificate2(cert),
                RemoteEndpoint = remoteEndpoint.ToString(),
                Ski = skiString

            }) ?? false;
            return result;
        }

        private async Task StartStandaloneInternalAsync(int port, CancellationToken cancellationToken)
        {
            // Load the server certificate exactly once. Without this, both
            // ConfigureHttpsDefaults and OnAuthenticate would call GenerateCert
            // per TLS handshake - hitting the disk, taking the generator's static
            // lock and leaking a fresh X509Certificate2 native handle every time.
            _serverCertificate ??= CertificateGenerator.GenerateCert(_settings.BasePath, _settings.Certificate);
            var serverCertificate = _serverCertificate;

            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
            builder.WebHost.UseKestrel();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ConfigureHttpsDefaults(httpOptions =>
                {
                    httpOptions.ServerCertificate = serverCertificate;

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
                        authenticationOptions.ServerCertificate = serverCertificate;
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
                bool connectedFired = false;
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

                    var cert = httpContext.Connection.ClientCertificate;
                    byte[] hash = SHA1.HashData(cert?.GetPublicKey() ?? []);
                    var ski = new SKI(hash);

                    // If a Server is already registered for this SKI, notify the
                    // manager that the old connection is going away before we
                    // create the replacement. The new Server's constructor will
                    // close the previous instance in the background, but the
                    // manager owns the Connected/Disconnected bookkeeping and
                    // must be told explicitly - otherwise the old entry leaks
                    // in _connections (different RemoteHost) and no
                    // OnDeviceConnectionStatusChanged(Unknown) is fired.
                    var previous = Server.Get(ski.ToString());
                    if (previous != null && OnDeviceConnectionChanged != null)
                    {
                        await OnDeviceConnectionChanged(new DeviceConnectionChangedEventArgs() { Connection = previous, ChangeType = DeviceConnectionChangeType.Disconnected });
                    }

                    var socket = await httpContext.WebSockets.AcceptWebSocketAsync("ship").ConfigureAwait(false);
                    if (socket == null || socket.State != WebSocketState.Open)
                    {
                        Console.WriteLine("Failed to accept socket from " + httpContext.Request.Host.ToUriComponent());
                        return;
                    }

                    server = new Server(ski.ToString(), httpContext.Request.Host, socket, this.devices, _logger);
                    if (OnDeviceConnectionChanged != null)
                    {
                        connectedFired = true;
                        await OnDeviceConnectionChanged(new DeviceConnectionChangedEventArgs() { Connection = server, ChangeType = DeviceConnectionChangeType.Connected });
                    }
                    await server.Do(_cts.Token).ConfigureAwait(false);

                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error while processing EEBUS websocket request.");
                    Console.WriteLine("Exception: " + ex.Message);

                    httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await httpContext.Response.WriteAsync("Error while processing websocket: " + ex.Message).ConfigureAwait(false);
                }
                finally
                {
                    // Only fire Disconnected for connections we actually fired
                    // Connected for. Without this guard the old code would emit
                    // a spurious Disconnected event for the previous Server we
                    // briefly held a reference to on the replace-existing path,
                    // and for early returns that never produced a live server.
                    if (server != null && connectedFired && OnDeviceConnectionChanged != null)
                    {
                        await OnDeviceConnectionChanged(new DeviceConnectionChangedEventArgs() { Connection = server, ChangeType = DeviceConnectionChangeType.Disconnected });
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
