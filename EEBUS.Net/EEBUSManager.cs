using EEBUS.DataStructures;
using EEBUS.Features;
using EEBUS.KeyValues;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Data.DataStructures;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.Net.Events;
using EEBUS.Net.Extensions;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using EEBUS.StateMachines;
using EEBUS.UseCases;
using EEBUS.UseCases.ControllableSystem;
using Makaretu.Dns;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Security;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
        public Func<LimitDataChangedEventArgs, Task>? OnLimitDataChanged;
        public Func<DeviceData, Task>? OnDeviceDataChanged { get; set; }
        public Func<RemoteDevice, DeviceConnectionStatus, Task>? OnDeviceConnectionStatusChanged { get; set; }

        private Func<NewConnectionValidationEventArgs, bool>? _onNewConnectionValidation = (NewConnectionValidationEventArgs args) => true;

        private CancellationTokenSource _cts = new();
        private CancellationTokenSource _clientCts = new();
        private X509Certificate2 _cert;

        public Devices Devices => _devices;

        internal List<Connection> Connections => _connections.Values.ToList();

        public EEBUSManager(Settings settings, Func<NewConnectionValidationEventArgs, bool>? onNewConnectionValidation = null, ServiceDiscovery? serviceDiscovery = null)
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
                                                 "EEBUS.UseCases.ControllableSystem", "EEBUS.UseCases.EnergyGuard", "EEBUS.UseCases.GridConnectionPoint", "EEBUS.UseCases.MonitoringAppliance",
                                                 "EEBUS.UseCases.MonitoredUnit", "EEBUS.Features" })
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

            lpcEventHandler = new LpcLimitStateMachine(localDevice);
            lpcEventHandler.RegisterEventHandler(new LPCEventHandler(this));
            lppEventHandler = new LppLimitStateMachine(localDevice);
            lppEventHandler.RegisterEventHandler(new LPPEventHandler(this));

            monitoringUseCasesEventHandler = new MonitoringUseCasesEventHandler(this);
            notifyEventHandler = new NotifyEventHandler(this);
            deviceConnectionStatusEventHandler = new DeviceConnectionStatusEventHandler(this);
            _devices.Local.AddUseCaseEvents(this.lpcEventHandler);
            _devices.Local.AddUseCaseEvents(this.lppEventHandler);
            _devices.Local.AddUseCaseEvents(this.notifyEventHandler);
            _devices.Local.AddUseCaseEvents(this.monitoringUseCasesEventHandler);
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
            Connection? connection = _connections.Values.FirstOrDefault(c => c.Remote?.SKI.ToString() == ski);
            if (connection != null)
            {
                return connection.ConnectionStatus;
            }

            return DeviceConnectionStatus.Unknown;
        }

        private LpcLimitStateMachine lpcEventHandler;
        private LppLimitStateMachine lppEventHandler;
        private MonitoringUseCasesEventHandler monitoringUseCasesEventHandler;
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
                        SpineDatagramPayload reply = new SpineDatagramPayload();
                        reply.datagram.header.addressSource = serverAddress;
                        reply.datagram.header.addressDestination = clientAddress;
                        reply.datagram.header.msgCounter = DataMessage.NextCount;
                        reply.datagram.header.cmdClassifier = "notify";

                        reply.datagram.payload = payload;
                        DataMessage dataMessage = new DataMessage();
                        dataMessage.SetPayload(JsonSerializer.SerializeToNode(reply) ?? throw new Exception("Failed to serialize data message"));
                        connection.PushDataMessage(dataMessage);
                    }
                }
            }
        }

        private class DeviceConnectionStatusEventHandler(EEBUSManager EEBusManager) : DeviceConnectionStatusEvents
        {
            public async Task DeviceConnectionStatusUpdatedAsync(Connection connection)
            {
                if (connection.ConnectionStatus == DeviceConnectionStatus.Connected)
                {
                    connection.ReadAndSubscribe();
                }

                if (EEBusManager.OnDeviceConnectionStatusChanged != null && connection.Remote != null)
                {
                    await EEBusManager.OnDeviceConnectionStatusChanged(connection.Remote, connection.ConnectionStatus);
                }
            }
        }

        private class MonitoringUseCasesEventHandler(EEBUSManager eebusManager) : MonitoringUseCaseEvents
        {
            public async Task DataUpdateMeasurementsAsync(List<MeasurementData.MeasurementData> measurementData, string ski)
            {
                MeasurementsData measurements = measurementData.CollectData();

                if (eebusManager.OnDeviceDataChanged != null)
                {
                    await eebusManager.OnDeviceDataChanged(new DeviceData
                    {
                        SKI = ski,
                        //Mgcp = mgcpData,
                        //Mpc = mpcData
                        Measurements = measurements
                    });
                }
            }

            public async Task DeviceConfigurationChangedAsync(Device device)
            {
                MgcpData? mgcpData = null;
                KeyValue? pvCurtailmentLimitFactor = device.KeyValues.FirstOrDefault(kv => kv.KeyName == "pvCurtailmentLimitFactor");
                if (pvCurtailmentLimitFactor != null)
                {
                    mgcpData = new MgcpData
                    {
                        PvCurtailmentLimitFactor = pvCurtailmentLimitFactor.Data.value.scaledNumber?.number
                    };
                }

                if (eebusManager.OnDeviceDataChanged != null)
                {
                    await eebusManager.OnDeviceDataChanged(new DeviceData
                    {
                        SKI = device.SKI.ToString(),
                        Mgcp = mgcpData
                    });
                }
            }
        }

        private class LPCEventHandler(EEBUSManager EEBusManager) : ILimitStateMachineEvents
        {
            public Task<WriteApprovalResult> ApproveActiveLimitWriteAsync(ActiveLimitWriteRequest request)
            {
                Console.WriteLine($"LPC Active Limit Write Request: Value={request.Value}, Active={request.IsLimitActive}");
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public Task<WriteApprovalResult> ApproveFailsafeLimitWriteAsync(FailsafeLimitWriteRequest request)
            {
                Console.WriteLine($"LPC Failsafe Limit Write Request: Value={request.Value}");
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public Task<WriteApprovalResult> ApproveFailsafeMinimumDurationWriteAsync(FailsafeDurationWriteRequest request)
            {
                Console.WriteLine($"Failsafe Duration Write Request: Duration={request.Duration}");
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public async Task OnEffectiveLimitChanged(EffectiveLimit limit)
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
                        Lpc = new LpcLppData
                        {
                            LimitActive = limit.IsLimited,
                            Limit = limit.Value,
                            LimitDuration = (int?)((limit.ExpiresAt?.Subtract(DateTimeOffset.Now))?.Duration().TotalSeconds)
                        }
                    };
                    await changedCallback(deviceData);
                }
            }
        }

        private class LPPEventHandler(EEBUSManager EEBusManager) : ILimitStateMachineEvents
        {
            public Task<WriteApprovalResult> ApproveActiveLimitWriteAsync(ActiveLimitWriteRequest request)
            {
                Console.WriteLine($"LPP Active Limit Write Request: Value={request.Value}, Active={request.IsLimitActive}");
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public Task<WriteApprovalResult> ApproveFailsafeLimitWriteAsync(FailsafeLimitWriteRequest request)
            {
                Console.WriteLine($"LPP Failsafe Limit Write Request: Value={request.Value}");
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public Task<WriteApprovalResult> ApproveFailsafeMinimumDurationWriteAsync(FailsafeDurationWriteRequest request)
            {
                Console.WriteLine($"Failsafe Duration Write Request: Duration={request.Duration}");
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public async Task OnEffectiveLimitChanged(EffectiveLimit limit)
            {
                //using var _ = Push(new LimitDataChanged(false, active, limit, duration));
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

        public DeviceData GetLocalData()
        {
            LocalDevice? local = _devices?.Local;

            if (null == local)
                return new();

            bool lpcActive = false;
            long lpcLimit = 0;
            TimeSpan lpcDuration = new();
            long lpcFailsafeLimit = 0;
            long lpcContractualNominalMax = 0;

            bool lppActive = false;
            long lppLimit = 0;
            TimeSpan lppDuration = new();
            long lppFailsafeLimit = 0;
            long lppContractualNominalMax = 0;

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

            foreach (ElectricalConnectionCharacteristicDataStructure data in local.GetDataStructures<ElectricalConnectionCharacteristicDataStructure>())
            {
                if (data.CharacteristicType == "contractualConsumptionNominalMax")
                {
                    lpcContractualNominalMax = data.Number;
                }
                else if (data.CharacteristicType == "contractualConsumptionNominalMax")
                {
                    lppContractualNominalMax = data.Number;
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

            MeasurementsData? measurements = null;
            AddressType? address = local.GetFeatureAddress("Measurement", true);
            if (address != null)
            {
                Entity? entity = local.Entities.FirstOrDefault(e => e.Index.SequenceEqual(address.entity));
                MeasurementServerFeature? measurementFeature = entity?.Features.FirstOrDefault(f => f.Index == address.feature) as MeasurementServerFeature;
                measurements = measurementFeature?.measurementData.CollectData();
            }

            return new DeviceData
            {
                SKI = local.SKI.ToString(),
                Lpc = new LpcLppData
                {
                    LimitActive = lpcActive,
                    Limit = lpcLimit,
                    LimitDuration = (int)lpcDuration.TotalSeconds,
                    FailSafeLimit = lpcFailsafeLimit,
                    ContractualNominalMax = lpcContractualNominalMax
                },
                Lpp = new LpcLppData
                {
                    LimitActive = lppActive,
                    Limit = lppLimit,
                    LimitDuration = (int)lppDuration.TotalSeconds,
                    FailSafeLimit = lppFailsafeLimit,
                    ContractualNominalMax = lppContractualNominalMax
                },
                Measurements = measurements,
                FailSafeLimitDuration = (int)failsafeDuration.TotalSeconds,
                UseCaseSupport = local.GetUseCaseSupport()
            };
        }

        public DeviceData GetRemoteData(string ski)
        {
            RemoteDevice? remote = _devices.Remote.FirstOrDefault(r => r.SKI.ToString() == ski);
            if (remote == null) return new();

            MeasurementsData? measurements = null;
            AddressType? address = remote.GetFeatureAddress("Measurement", true);
            if (address != null)
            {
                Entity? entity = remote.Entities.FirstOrDefault(e => e.Index.SequenceEqual(address.entity));
                MeasurementServerFeature? measurementFeature = entity?.Features.FirstOrDefault(f => f.Index == address.feature) as MeasurementServerFeature;
                if (measurementFeature != null)
                {
                    measurements = measurementFeature.measurementData.CollectData();
                }
            }

            MgcpData mgcpData = new();
            KeyValue? pvCurtailmentKeyValue = remote.KeyValues.FirstOrDefault(kv => kv.KeyName == "pvCurtailmentLimitFactor");
            mgcpData.PvCurtailmentLimitFactor = pvCurtailmentKeyValue?.Data.value.scaledNumber?.number;

            return new DeviceData
            {
                SKI = remote.SKI.ToString(),
                Measurements = measurements,
                Mgcp = mgcpData,
                UseCaseSupport = remote.GetUseCaseSupport(),
                Name = remote.Name,
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

        public IEnumerable<RemoteDeviceData> GetConnectedDevices()
        {
            return _devices.Remote.Select(rd => new RemoteDeviceData
            {
                Id = rd.Id,
                Name = rd.Name,
                Ski = rd.SKI.ToString(),
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

            var deviceConfigurationKeyValueListData = SpineCmdPayloadBase.GetClass("deviceConfigurationKeyValueListData");
            if (deviceConfigurationKeyValueListData != null)
            {
                await deviceConfigurationKeyValueListData.WriteDataAsync(_devices.Local, deviceData);
            }

            if (deviceData.Measurements != null)
            {
                var measurementListData = SpineCmdPayloadBase.GetClass("measurementListData");
                if (measurementListData != null)
                {
                    await measurementListData.WriteDataAsync(_devices.Local, deviceData);
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
                        //Debug.WriteLine(hostString.ToString());

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
            _shipListener.OnDeviceConnectionChanged = OnDeviceConnectionChangedAsync;
            _shipListener.StartAsync(_settings.Device.Port);
        }

        private async Task OnDeviceConnectionChangedAsync(DeviceConnectionChangedEventArgs e)
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
                    if (OnDeviceConnectionStatusChanged != null)
                    {
                        await OnDeviceConnectionStatusChanged(removedConnection.Remote, DeviceConnectionStatus.Unknown);
                    }
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
            //_shipListener?.OnDeviceConnectionChanged -= _shipListener_OnDeviceConnectionChanged;

            _devices.RemoteDeviceFound -= OnRemoteDeviceFound;
            _devices.ServerStateChanged -= OnServerStateChanged;
            _devices.ClientStateChanged -= OnClientStateChanged;

            _devices.Local.RemoveUseCaseEvents(this.lpcEventHandler);
            _devices.Local.RemoveUseCaseEvents(this.lppEventHandler);
            _devices.Local.RemoveUseCaseEvents(this.notifyEventHandler);
            _devices.Local.RemoveUseCaseEvents(this.monitoringUseCasesEventHandler);
            _devices.Local.RemoveUseCaseEvents(this.deviceConnectionStatusEventHandler);

            _cts.Cancel();
            _clientCts.Cancel();

            if (_serviceDiscoveryNeedsDispose)
            {
                _serviceDiscovery?.Dispose();
            }
        }

        //For debug only
        //public void SendRead()
        //{
        //    var conn = Connections.First();
        //    var source = conn.Local.GetFeatureAddress("Measurement", false);
        //    var dest = conn.Remote?.GetFeatureAddress("Measurement", true);
        //    if (source == null || dest == null) return;

        //    //var message = DataMessage.CreateRead(source, dest, SpineCmdPayloadBase.GetClass("measurementDescriptionListData")?.CreateRead(conn));
        //    var message = DataMessage.CreateSubscription(source, dest, "Measurement", conn.Local.DeviceId, conn.Remote?.DeviceId ?? string.Empty);
        //    conn.PushDataMessage(message);
        //}

    }
}
