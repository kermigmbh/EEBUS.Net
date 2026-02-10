using EEBUS.DataStructures;
using EEBUS.KeyValues;
using EEBUS.Messages;
using EEBUS.Models;
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
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
        public event EventHandler<LimitDataChangedEventArgs>? OnLimitDataChanged;

        private CancellationTokenSource _cts = new();
        private CancellationTokenSource _clientCts = new();
        private X509Certificate2 _cert;

        public Devices Devices => _devices;

        public EEBUSManager(Settings settings, ServiceDiscovery? serviceDiscovery = null)
        {

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

            _cert = CertificateGenerator.GenerateCert(settings.Certificate);

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
            _devices.Local.AddUseCaseEvents(this.lpcEventHandler);
            _devices.Local.AddUseCaseEvents(this.lppEventHandler);
            _devices.Local.AddUseCaseEvents(this.lpcOrLppEventHandler);
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

        private LPCEventHandler lpcEventHandler;
        private LPPEventHandler lppEventHandler;
        private LPCorLPPEventHandler lpcOrLppEventHandler;

        private class LPCEventHandler(EEBUSManager EEBusManager) : LPCEvents
        {
            public void DataUpdateLimit(int counter, bool active, long limit, TimeSpan duration)
            {
                //using var _ = Push(new LimitDataChanged(true, active, limit, duration));
                Console.WriteLine("UpdateLimit");
                EEBusManager.OnLimitDataChanged?.Invoke(EEBusManager, new LimitDataChangedEventArgs() { IsLPC = true, IsActive = active, Limit = limit, Duration = duration });
            }

            public void DataUpdateFailsafeConsumptionActivePowerLimit(int counter, long limit)
            {
                //using var _ = Push(new FailsafeLimitDataChanged(true, limit));
            }
        }

        private class LPPEventHandler(EEBUSManager EEBusManager) : LPPEvents
        {
            public void DataUpdateLimit(int counter, bool active, long limit, TimeSpan duration)
            {
                //using var _ = Push(new LimitDataChanged(false, active, limit, duration));
            }

            public void DataUpdateFailsafeProductionActivePowerLimit(int counter, long limit)
            {
                //using var _ = Push(new FailsafeLimitDataChanged(false, limit));
            }
        }

        private class LPCorLPPEventHandler(EEBUSManager EEBusManager) : LPCorLPPEvents
        {
            public void DataUpdateFailsafeDurationMinimum(int counter, TimeSpan duration)
            {
                //using var _ = Push(new FailsafeLimitDurationChanged(duration));
            }

            public void DataUpdateHeartbeat(int counter, RemoteDevice device, uint timeout)
            {
                //using var _ = Push(new HeartbeatReceived(device, timeout));
            }
        }

        public JsonObject? GetLocal()
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

            FailsafeConsumptionActivePowerLimitKeyValue lpcFailsafeLimitKeyValue = local.GetKeyValue<FailsafeConsumptionActivePowerLimitKeyValue>();
            if (null != lpcFailsafeLimitKeyValue)
                lpcFailsafeLimit = lpcFailsafeLimitKeyValue.Value;

            FailsafeProductionActivePowerLimitKeyValue lppFailsafeLimitKeyValue = local.GetKeyValue<FailsafeProductionActivePowerLimitKeyValue>();
            if (null != lppFailsafeLimitKeyValue)
                lppFailsafeLimit = lppFailsafeLimitKeyValue.Value;

            FailsafeDurationMinimumKeyValue failsafeDurationKeyValue = local.GetKeyValue<FailsafeDurationMinimumKeyValue>();
            if (null != lppFailsafeLimitKeyValue)
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
            var json = JsonSerializer.SerializeToNode(payload, options)?.AsObject();
            return json;

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

        public DataStructure? GetLocalData(string type)
        {
            LocalDevice? local = _devices?.Local;
            if (local == null) return null;

            DataStructure? structure = local.GetDataStructures(type).FirstOrDefault();
            return structure;
        }

        //public void SendReadMessage(string host, int port, int[] sourceEntityAddress, int sourceFeatureIndex, SKI targetSki, int[] destinationEntityAddress, int destinationFeatureIndex, string payloadType)
        //{
        //    RemoteDevice? remote = _devices.Remote.FirstOrDefault(r => r.SKI == targetSki);
        //    if (remote == null) return;

        //    AddressType source = new AddressType { device = _devices.Local.DeviceId, entity = sourceEntityAddress, feature = sourceFeatureIndex };
        //    AddressType destination = new AddressType { device = remote.DeviceId, entity = destinationEntityAddress, feature = destinationFeatureIndex };
        //    HostString hs = new HostString(host, port);

        //    Connection? activeConnection = null;

        //    activeConnection = Server.Get(hs);  //Try to either get the server...
        //    if (activeConnection == null)
        //    {
        //        activeConnection = _clients.GetValueOrDefault(hs);  //...or the client connection
        //    }

        //    if (activeConnection == null) return;

        //    SpineDatagramPayload read = new SpineDatagramPayload();
        //    read.datagram.header.addressSource = source;
        //    read.datagram.header.addressDestination = destination;
        //    read.datagram.header.msgCounter = DataMessage.NextCount;
        //    read.datagram.header.cmdClassifier = "read";

        //   // var payload = new DeviceDiagnosisHeartbeatData.Class().CreateRead(activeConnection);
        //    var cmdClass = SpineCmdPayloadBase.GetClass(payloadType);

        //    read.datagram.payload = payload.ToJsonNode();// JsonSerializer.SerializeToNode(heartbeatReadPayload);

        //    DataMessage message = new DataMessage();
        //    message.SetPayload(JsonSerializer.SerializeToNode(read) ?? throw new Exception("Failed to serialize read message"));

        //    activeConnection.PushDataMessage(message);
        //}

        //public JsonArray GetRemotes()
        //{
        //    JsonArray devlist = new();

        //    _devices.Remote.ForEach(rd =>
        //    {
        //        devlist.Add( new
        //        {
        //            id = rd.Id,
        //            name = rd.Name,
        //            ski = rd.SKI.ToReadable(),
        //            url = rd.Url,
        //            serverState = rd.serverState.ToString(),
        //            clientState = rd.clientState.ToString()
        //        });
        //    });

        //    return devlist;

        //}


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
                    wsClient.Options.RemoteCertificateValidationCallback = (object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) => true;
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
                    await removedClient.CloseAsync();
                }
            }
        }



        public void Start()
        {
            _shipListener = new SHIPListener(_devices);
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
                _connections.TryRemove(e.Connection.RemoteHost, out Connection? removedremovedConnection);
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
