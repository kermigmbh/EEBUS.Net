using EEBUS.Models;
using EEBUS.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Net.Security;
using System.Runtime.ConstrainedExecution;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace EEBUS.vue.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            var services = builder.Services;
            var settings = builder.Configuration.GetSection("Settings");

            services.Configure<KestrelServerOptions>(kestrelOptions =>
            {
                kestrelOptions.ConfigureHttpsDefaults(httpOptions =>
                {
                    httpOptions.ServerCertificate = CertificateGenerator.GenerateCert(settings.Get<Settings>()?.Certificate);
                    httpOptions.ClientCertificateMode = ClientCertificateMode.NoCertificate;
                    httpOptions.ClientCertificateValidation = ValidateClientCert;
                    httpOptions.SslProtocols = SslProtocols.Tls12;
                    httpOptions.OnAuthenticate = (connectionContext, authenticationOptions) =>
                    {
                        authenticationOptions.EnabledSslProtocols = SslProtocols.Tls12;
                    };
                });
            });

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", builder =>
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader());
            });
            services.Configure<Settings>(settings);

            services.AddControllers().AddNewtonsoftJson();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            Devices devices = new Devices();
            MDNSClient mDNSClient = new MDNSClient();
            MDNSService mDNSService = new MDNSService(settings);

            services.AddSingleton<Settings>();
            services.AddSingleton<Devices>(devices);
            services.AddSingleton<MDNSClient>(mDNSClient);
            services.AddSingleton<MDNSService>(mDNSService);

            var s = settings.Get<Settings>();
            var cert = CertificateGenerator.GenerateCert(s.Certificate);

            services.AddSingleton<X509Certificate2>(cert);

            var app = builder.Build();

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors("AllowAll");

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            var webSocketOptions = new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(50)
            };

            app.UseWebSockets(webSocketOptions);

            app.MapControllers();

            app.MapFallbackToFile("/index.html");


            //app.UseMiddleware<SHIPMiddleware>();

           
            var manager = new EEBUSManager(s);
            manager.StartServer();


            manager.StartDeviceSearch();





            // start our mDNS services
            //mDNSClient.Run(devices);


          

            //byte[] hash = SHA1.Create().ComputeHash(cert.GetPublicKey());

            //LocalDevice localDevice = devices.GetOrCreateLocal(hash, s.Device);

            //mDNSService.Run(localDevice, default);



            app.Run();
        }

        private static bool ValidateClientCert(X509Certificate2 certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            // auto accept mode is active, register flag is set in discovery service
            return true;
        }


    }
}
