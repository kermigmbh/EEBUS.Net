using EEBUS.DataStructures;
using EEBUS.KeyValues;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.Net.Events;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using EEBUS.UseCases.ControllableSystem;
using Makaretu.Dns;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Xml;

namespace EEBUS.Net
{
    public class EEBUSManager : IDisposable
    {

        private ConcurrentDictionary<HostString, Connection> _connections = new();
        private Devices _devices;
        private readonly MDNSClient _mDNSClient;
        private readonly MDNSService _mDNSService;
        SHIPListener? _shipListener;
        private readonly Settings _settings;
        private readonly ServiceDiscovery? _serviceDiscovery;
        private bool _serviceDiscoveryNeedsDispose = false;

        public event EventHandler<RemoteDevice>? OnDeviceFound;
        //public event EventHandler<LimitDataChangedEventArgs>? OnLimitDataChanged;
        public Func<LimitDataChangedEventArgs, Task>? OnLimitDataChanged;
        public Func<DeviceData, Task>? OnDeviceDataChanged { get; set; }
        public Func<RemoteDevice, DeviceConnectionStatus, Task>? OnDeviceConnectionStatusChanged { get; set; }

        private Func<NewConnectionValidationEventArgs, bool>? _onNewConnectionValidation  = (NewConnectionValidationEventArgs args) => true;
        private ConcurrentDictionary<string, DeviceConnectionStatus> _deviceConnectionStatus = new();

        private CancellationTokenSource _cts = new();
        private CancellationTokenSource _clientCts = new();
        private X509Certificate2 _cert;

        public Devices Devices => _devices;

        internal List<Connection> Connections => _connections.Values.ToList();

        public EEBUSManager(Settings settings, Func<NewConnectionValidationEventArgs, bool>? onNewConnectionValidation = null,  ServiceDiscovery? serviceDiscovery = null)
        {
            if (onNewConnectionValidation != null)
            {
                _onNewConnectionValidation = onNewConnectionValidation;
            }
            if (serviceDiscovery == null)
            {
                _serviceDiscoveryNeedsDispose = true;
                serviceDiscovery = new ServiceDiscovery();
            }

            foreach (string ns in new string[] {"EEBUS.SHIP.Messages", "EEBUS.SPINE.Commands", "EEBUS.Entities",
                                                 "EEBUS.UseCases.ControllableSystem", "EEBUS.UseCases.GridConnectionPoint",
                                                 "EEBUS.Features" })
            {
                foreach (Type type in GetTypesInNamespace(typeof(Settings).Assembly, ns))
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }


            _devices = new Devices();
            _mDNSClient = new MDNSClient(serviceDiscovery);

            _cert = CertificateGenerator.GenerateCert(settings.BasePath, settings.Certificate);

            byte[] hash = SHA1.Create().ComputeHash(_cert.GetPublicKey());

            this._mDNSService = new MDNSService(settings.Device.Id, settings.Device.Port, serviceDiscovery);

            LocalDevice localDevice = _devices.GetOrCreateLocal(hash, settings.Device);

            _mDNSService.Run(localDevice, _cts.Token);
            _mDNSClient.Run(_devices);
            _devices.RemoteDeviceFound += OnRemoteDeviceFound;
            _devices.ServerStateChanged += OnServerStateChanged;
            _devices.ClientStateChanged += OnClientStateChanged;

            lpcEventHandler = new LPCEventHandler(this);
            lppEventHandler = new LPPEventHandler(this);
            lpcOrLppEventHandler = new LPCorLPPEventHandler(this);
            notifyEventHandler = new NotifyEventHandler(this);
            deviceConnectionStatusEventHandler = new DeviceConnectionStatusEventHandler(this);
            _devices.Local.AddUseCaseEvents(this.lpcEventHandler);
            _devices.Local.AddUseCaseEvents(this.lppEventHandler);
            _devices.Local.AddUseCaseEvents(this.lpcOrLppEventHandler);
            _devices.Local.AddUseCaseEvents(this.notifyEventHandler);
            _devices.Local.AddUseCaseEvents(this.deviceConnectionStatusEventHandler);
            _settings = settings;
            this._serviceDiscovery = serviceDiscovery;
        }
        private static Type[] GetTypesInNamespace(Assembly assembly, string nameSpace)
        {
            return assembly.GetTypes()
                            .Where(t => String.Equals(t.Namespace, nameSpace, StringComparison.Ordinal))
                            .ToArray();
        }


        private void OnRemoteDeviceFound(RemoteDevice device)
        {
            //using var _ = Push(new RemoteDeviceFound(device));
            OnDeviceFound?.Invoke(this, device);
        }

        private void OnServerStateChanged(Connection.EState state, RemoteDevice device)
        {
            //using var _ = Push(new ServerStateChanged(device, state));
        }

        private void OnClientStateChanged(Connection.EState state, RemoteDevice device)
        {
            //using var _ = Push(new ClientStateChanged(device, state));
        }

        public DeviceConnectionStatus GetConnectionStatus(string ski)
        {
            if (_deviceConnectionStatus.ContainsKey(ski))
            {
                return _deviceConnectionStatus[ski];
            }
            return DeviceConnectionStatus.Unknown;
        }

        internal void UpdateConnectionStatus(string ski, DeviceConnectionStatus connectionStatus)
        {
            _deviceConnectionStatus[ski] = connectionStatus;
        }

        private LPCEventHandler lpcEventHandler;
        private LPPEventHandler lppEventHandler;
        private LPCorLPPEventHandler lpcOrLppEventHandler;
        private NotifyEventHandler notifyEventHandler;
        private DeviceConnectionStatusEventHandler deviceConnectionStatusEventHandler;
        private class NotifyEventHandler(EEBUSManager EEBusManager) : NotifyEvents
        {
            public async Task NotifyAsync(JsonNode? payload, AddressType localFeatureAddress)
            {
                if (payload == null) return;
                AddressType serverAddress = localFeatureAddress;

                foreach (Connection connection in EEBusManager.Connections)
                {
                    IEnumerable<AddressType> clientAddresses = connection.BindingAndSubscriptionManager.GetSubscriptionsByServerAddress(serverAddress);

                    foreach (var clientAddress in clientAddresses)
                    {
                        //if (connection.BindingAndSubscriptionManager.HasSubscription(clientAddress, serverAddress))
                        //{
                            SpineDatagramPayload reply = new SpineDatagramPayload();
                            reply.datagram.header.addressSource = serverAddress;
                            reply.datagram.header.addressDestination = clientAddress;
                            reply.datagram.header.msgCounter = DataMessage.NextCount;
                            reply.datagram.header.cmdClassifier = "notify";

                            reply.datagram.payload = payload;
                            DataMessage dataMessage = new DataMessage();
                            dataMessage.SetPayload(JsonSerializer.SerializeToNode(reply) ?? throw new Exception("Failed to serialize data message"));
                            connection.PushDataMessage(dataMessage);
                        //}
                    }
                }
            }
        }

        private class DeviceConnectionStatusEventHandler(EEBUSManager EEBusManager) : DeviceConnectionStatusEvents
        {
            public Task RemoteDiscoveryCompletedAsync(RemoteDevice remoteDevice)
            {
                EEBusManager.UpdateConnectionStatus(remoteDevice.SKI.ToString(), DeviceConnectionStatus.DiscoveryCompleted);
                var callback = EEBusManager.OnDeviceConnectionStatusChanged;
                if (callback != null)
                {
                    return callback(remoteDevice, DeviceConnectionStatus.DiscoveryCompleted);
                }
                return Task.CompletedTask;
            }
        }

        private class LPCEventHandler(EEBUSManager EEBusManager) : LPCEvents
        {
            public async Task DataUpdateLimitAsync(int counter, bool active, long limit, TimeSpan duration, string remoteSki)
            {
                //using var _ = Push(new LimitDataChanged(true, active, limit, duration));
                Console.WriteLine("UpdateLimit");
                //var changedCallback = EEBusManager.OnLimitDataChanged;
                //if (changedCallback is not null)
                //{
                //    await changedCallback(new LimitDataChangedEventArgs() { IsLPC = true, IsActive = active, Limit = limit, Duration = duration });
                //}

                var changedCallback = EEBusManager.OnDeviceDataChanged;
                if (changedCallback != null)
                {
                    var deviceData = new DeviceData
                    {
                        SKI = remoteSki,
                        Lpc = new LpcLppData
                        {
                            LimitActive = active,
                            Limit = limit,
                            LimitDuration = (int)duration.TotalSeconds
                        }
                    };
                    await changedCallback(deviceData);
                }
            }

            public async Task DataUpdateFailsafeConsumptionActivePowerLimitAsync(int counter, long limit, string remoteSki)
            {
                //using var _ = Push(new FailsafeLimitDataChanged(true, limit));

            }
        }

        private class LPPEventHandler(EEBUSManager EEBusManager) : LPPEvents
        {
            public async Task DataUpdateLimitAsync(int counter, bool active, long limit, TimeSpan duration, string remoteSki)
            {
                //using var _ = Push(new LimitDataChanged(false, active, limit, duration));
            }

            public async Task DataUpdateFailsafeProductionActivePowerLimitAsync(int counter, long limit, string remoteSki)
            {
                //using var _ = Push(new FailsafeLimitDataChanged(false, limit));
            }
        }

        private class LPCorLPPEventHandler(EEBUSManager EEBusManager) : LPCorLPPEvents
        {
            public async Task DataUpdateFailsafeDurationMinimumAsync(int counter, TimeSpan duration, string remoteSki)
            {
                //using var _ = Push(new FailsafeLimitDurationChanged(duration));
            }

            public async Task DataUpdateHeartbeatAsync(int counter, RemoteDevice device, uint timeout, string remoteSki)
            {
                //using var _ = Push(new HeartbeatReceived(device, timeout));
            }
        }

        public JsonObject GetLocal()
        {
            LocalDevice? local = _devices?.Local;

            if (null == local)
                return new();

            bool lpcActive = false;
            long lpcLimit = 0;
            TimeSpan lpcDuration = new();
            long lpcFailsafeLimit = 0;

            bool lppActive = false;
            long lppLimit = 0;
            TimeSpan lppDuration = new();
            long lppFailsafeLimit = 0;

            TimeSpan failsafeDuration = new();

            foreach (LoadControlLimitDataStructure data in local.GetDataStructures<LoadControlLimitDataStructure>())
            {
                if (data.LimitDirection == "consume")
                {
                    lpcActive = data.LimitActive;
                    lpcLimit = data.Number;
                    lpcDuration = data.EndTime == null ? Timeout.InfiniteTimeSpan : XmlConvert.ToTimeSpan(data.EndTime);
                }
                else if (data.LimitDirection == "produce")
                {
                    lppActive = data.LimitActive;
                    lppLimit = data.Number;
                    lppDuration = data.EndTime == null ? Timeout.InfiniteTimeSpan : XmlConvert.ToTimeSpan(data.EndTime);
                }
            }

            FailsafeConsumptionActivePowerLimitKeyValue? lpcFailsafeLimitKeyValue = local.GetKeyValue<FailsafeConsumptionActivePowerLimitKeyValue>();
            if (null != lpcFailsafeLimitKeyValue)
                lpcFailsafeLimit = lpcFailsafeLimitKeyValue.Value;

            FailsafeProductionActivePowerLimitKeyValue? lppFailsafeLimitKeyValue = local.GetKeyValue<FailsafeProductionActivePowerLimitKeyValue>();
            if (null != lppFailsafeLimitKeyValue)
                lppFailsafeLimit = lppFailsafeLimitKeyValue.Value;

            FailsafeDurationMinimumKeyValue? failsafeDurationKeyValue = local.GetKeyValue<FailsafeDurationMinimumKeyValue>();
            if (null != failsafeDurationKeyValue)
                failsafeDuration = XmlConvert.ToTimeSpan(failsafeDurationKeyValue.Duration);

            var payload = new
            {
                name = local.Name,
                ski = local.SKI.ToReadable(),
                shipId = local.ShipID,

                lpcActive = lpcActive,
                lpcLimit = lpcLimit,
                lpcDuration = lpcDuration,
                lpcFailsafeLimit = lpcFailsafeLimit,

                //heartbeatTimeout = 


                lppActive = lppActive,
                lppLimit = lppLimit,
                lppDuration = lppDuration,
                lppFailsafeLimit = lppFailsafeLimit,

                failsafeDuration = failsafeDuration


            };


            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                // Include nulls (Newtonsoft included them by default)
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
                // If any values in the object are enums and you want them as strings:
                // Converters = { new JsonStringEnumConverter() }
            };

            // -> JSON string
            JsonObject json = JsonSerializer.SerializeToNode(payload, options)?.AsObject() ?? throw new Exception("Failed to serialize local device data");
            return json;

        }

        public DeviceData GetDeviceData()
        {
            LocalDevice? local = _devices?.Local;

            if (null == local)
                return new();

            bool lpcActive = false;
            long lpcLimit = 0;
            TimeSpan lpcDuration = new();
            long lpcFailsafeLimit = 0;

            bool lppActive = false;
            long lppLimit = 0;
            TimeSpan lppDuration = new();
            long lppFailsafeLimit = 0;

            TimeSpan failsafeDuration = new();

            foreach (LoadControlLimitDataStructure data in local.GetDataStructures<LoadControlLimitDataStructure>())
            {
                if (data.LimitDirection == "consume")
                {
                    lpcActive = data.LimitActive;
                    lpcLimit = data.Number;
                    lpcDuration = data.EndTime == null ? Timeout.InfiniteTimeSpan : XmlConvert.ToTimeSpan(data.EndTime);
                }
                else if (data.LimitDirection == "produce")
                {
                    lppActive = data.LimitActive;
                    lppLimit = data.Number;
                    lppDuration = data.EndTime == null ? Timeout.InfiniteTimeSpan : XmlConvert.ToTimeSpan(data.EndTime);
                }
            }

            FailsafeConsumptionActivePowerLimitKeyValue? lpcFailsafeLimitKeyValue = local.GetKeyValue<FailsafeConsumptionActivePowerLimitKeyValue>();
            if (null != lpcFailsafeLimitKeyValue)
                lpcFailsafeLimit = lpcFailsafeLimitKeyValue.Value;

            FailsafeProductionActivePowerLimitKeyValue? lppFailsafeLimitKeyValue = local.GetKeyValue<FailsafeProductionActivePowerLimitKeyValue>();
            if (null != lppFailsafeLimitKeyValue)
                lppFailsafeLimit = lppFailsafeLimitKeyValue.Value;

            FailsafeDurationMinimumKeyValue? failsafeDurationKeyValue = local.GetKeyValue<FailsafeDurationMinimumKeyValue>();
            if (null != failsafeDurationKeyValue)
                failsafeDuration = XmlConvert.ToTimeSpan(failsafeDurationKeyValue.Duration);

            return new DeviceData
            {
                Name = local.Name,
                SKI = local.SKI.ToString(),
                ShipId = local.ShipID,
                Lpc = new LpcLppData
                {
                    LimitActive = lpcActive,
                    Limit = lpcLimit,
                    LimitDuration = (int)lpcDuration.TotalSeconds,
                    FailSafeLimit = lpcFailsafeLimit
                },
                Lpp = new LpcLppData
                {
                    LimitActive = lppActive,
                    Limit = lppLimit,
                    LimitDuration = (int)lppDuration.TotalSeconds,
                    FailSafeLimit = lppFailsafeLimit
                },
                FailSafeLimitDuration = (int)failsafeDuration.TotalSeconds
            };
        }

        public JsonArray GetRemotes()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };

            var projection = _devices.Remote.Select(rd => new
            {
                id = rd.Id,
                name = rd.Name,
                ski = rd.SKI.ToReadable(),
                url = rd.Url,
                serverState = rd.serverState,
                clientState = rd.clientState,

            });

            // Root will be a JsonArray because projection is an IEnumerable<anonymous>
            return JsonSerializer.SerializeToNode(projection, options)!.AsArray();
        }

        public IEnumerable<RemoteDeviceData> GetRemoteData()
        {
            return _devices.Remote.Select(rd => new RemoteDeviceData
            {
                Id = rd.Id,
                Name = rd.Name,
                Ski = rd.SKI.ToString(),
                SupportsLpc = rd.SupportsUseCase("limitationOfPowerConsumption"),
                SupportsLpp = rd.SupportsUseCase("limitationOfPowerProduction")
            });
        }

        public async Task WriteDataAsync(DeviceData deviceData)
        {
            if (deviceData.Lpc != null || deviceData.Lpp != null)
            {
                var loadControlLimitListData = SpineCmdPayloadBase.GetClass("loadControlLimitListData");
                if (loadControlLimitListData != null)
                {
                    await loadControlLimitListData.WriteDataAsync(_devices.Local, deviceData);
                }
            }
           

            if (deviceData.FailSafeLimitDuration != null && deviceData.Lpc != null || deviceData.Lpp != null)
            {
                var deviceConfigurationKeyValueListData = SpineCmdPayloadBase.GetClass("deviceConfigurationKeyValueListData");
                if (deviceConfigurationKeyValueListData != null)
                {
                    await deviceConfigurationKeyValueListData.WriteDataAsync(_devices.Local, deviceData);
                }
            }

            
        }

        /// <summary>
        /// returns hoststring if success, otherwise null or exception
        /// </summary>
        /// <param name="ski"></param>
        /// <returns></returns>
        public async Task<string?> ConnectAsync(string ski)
        {
            SKI Ski = new SKI(ski);

            RemoteDevice? device = _devices.Remote.FirstOrDefault(rd => rd.SKI == Ski);
            if (null == device)
            {
                return null;
            }
            try
            {
                Uri uri = new Uri("wss://" + device.Url);
                HostString hostString = new HostString(uri.Host, uri.Port);

                ClientWebSocket? wsClient = null;
                if (!_connections.TryGetValue(hostString, out Connection? existingClient))
                {
                    wsClient = new ClientWebSocket();
                    wsClient.Options.AddSubProtocol("ship");
                    wsClient.Options.RemoteCertificateValidationCallback = (object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
                    {
                        if (cert == null)
                        {
                            return false;
                        }
                        Console.WriteLine(hostString.ToString());

                        byte[] hash = SHA1.Create().ComputeHash(cert.GetPublicKey() ?? []);
                        var ski = new SKI(hash);
                        var skiString = ski.ToString();

                        if (device.SKI.ToString() != skiString)
                        {
                            Console.WriteLine($"Certificate SKI {skiString} does not match device SKI {device.SKI.ToString()}");
                            return false;
                        }


                        return _onNewConnectionValidation?.Invoke(new NewConnectionValidationEventArgs()
                        {
                            Certificate = new X509Certificate2(cert),
                            RemoteEndpoint = hostString.ToString(),
                            Ski = skiString

                        }) ?? false;


                        
                    };
                    wsClient.Options.ClientCertificates.Add(_cert);
                    await wsClient.ConnectAsync(uri, CancellationToken.None).ConfigureAwait(false);
                    if (wsClient?.State == WebSocketState.Open)
                    {
                        Client client = new Client(hostString, wsClient, _devices, device);
                        _connections[hostString] = client;
                        await client.Run().ConfigureAwait(false);
                        return hostString.ToString();
                    }
                    else
                    {
                        await DisconnectAsync(hostString).ConfigureAwait(false);
                    }
                }
                else
                {
                    wsClient = existingClient.WebSocket as ClientWebSocket;
                    if (wsClient?.State == WebSocketState.Open)
                    {
                        return hostString.ToString();
                    }
                    else
                    {
                        await DisconnectAsync(hostString).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Connect Error: " + ex.Message);
                throw;
            }
            return null;
        }

        public async Task DisconnectAsync(HostString host)
        {
            var wsClient = _connections.TryGetValue(host, out Connection? client) ? client?.WebSocket : null;
            if (wsClient == null)
                return;

            try
            {
                // send close message
                CloseMessage closeMessage = new CloseMessage(ConnectionClosePhaseType.announce);
                await closeMessage.Send(wsClient);

                // wait for close response message from server
                closeMessage = await CloseMessage.Receive(wsClient);
                if (closeMessage == null)
                {
                    throw new Exception("Close message parsing failed!");
                }

                if (closeMessage.connectionClose[0].phase != ConnectionClosePhaseType.confirm)
                {
                    throw new Exception("Close confirmation message expected!");
                }

                // now close websocket
                await wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                Debug.WriteLine("Disconnect Error: " + ex.Message);
            }
            finally
            {
                //We need to dispose the websocket in any case, e.g. it can be that we send a close message, but we do not receive one from the remote partner. In this case, we must close the connection
                wsClient.Dispose();
                wsClient = null;
                if (_connections.TryRemove(host, out Connection? removedClient) && removedClient != null)
                {
                    if (removedClient.Remote != null)
                    {
                        _deviceConnectionStatus.TryRemove(removedClient.Remote.SKI.ToString(), out _);
                    }
                    await removedClient.CloseAsync();
                }
            }
        }




        public void Start()
        {
            _shipListener = new SHIPListener(_devices, _settings);
            _shipListener.OnNewConnectionValidation = (args) =>
            {
                RemoteDevice? device = _devices.Remote.FirstOrDefault(rd => rd.SKI.ToString() == args.Ski);
                if (null == device)
                {
                    Console.WriteLine($"remote device with SKI {args.Ski} has no mDNS advertisements");
                    return false;
                }
                

                return _onNewConnectionValidation?.Invoke(new NewConnectionValidationEventArgs()
                    {
                        Certificate = args.Certificate,
                        RemoteEndpoint = args.RemoteEndpoint,
                        Ski = args.Ski
    
                    }) ?? false;
            };
            _shipListener.OnDeviceConnectionChanged += _shipListener_OnDeviceConnectionChanged;
            _shipListener.StartAsync(_settings.Device.Port);
        }

        private void _shipListener_OnDeviceConnectionChanged(object? sender, DeviceConnectionChangedEventArgs e)
        {
            if (e.ChangeType == DeviceConnectionChangeType.Connected)
            {
                _connections[e.Connection.RemoteHost] = e.Connection;
            }
            else
            {
                _connections.TryRemove(e.Connection.RemoteHost, out Connection? removedConnection);
                if (removedConnection?.Remote != null)
                {
                    _deviceConnectionStatus.TryRemove(removedConnection.Remote.SKI.ToString(), out _);
                    
                }
            }
        }

        public void Stop()
        {

            _shipListener?.StopAsync();
            _mDNSClient.Stop();
        }

        public void Dispose()
        {
            _shipListener?.OnDeviceConnectionChanged -= _shipListener_OnDeviceConnectionChanged;

            _devices.RemoteDeviceFound -= OnRemoteDeviceFound;
            _devices.ServerStateChanged -= OnServerStateChanged;
            _devices.ClientStateChanged -= OnClientStateChanged;

            _devices.Local.RemoveUseCaseEvents(this.lpcEventHandler);
            _devices.Local.RemoveUseCaseEvents(this.lppEventHandler);
            _devices.Local.RemoveUseCaseEvents(this.lpcOrLppEventHandler);

            _cts.Cancel();
            _clientCts.Cancel();

            if (_serviceDiscoveryNeedsDispose)
            {
                _serviceDiscovery?.Dispose();
            }
        }
    }
}
