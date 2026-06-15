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
using EEBUS.StateMachines;
using EEBUS.UseCases;
using EEBUS.UseCases.ControllableSystem;
using Makaretu.Dns;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data.SqlTypes;
using System.Diagnostics;
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
using System.Xml;

namespace EEBUS.Net
{
    public class EEBUSManager : IDisposable
    {
        private const int ReconnectLoopIntervalMs = 10000;
        private ConcurrentDictionary<HostString, Connection> _connections = new();
        private List<string> _trustedSkis = new();
        private object _lock = new object();

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

        private CancellationTokenSource _cts = new();
        private X509Certificate2 _cert;

        public Devices Devices => _devices;

        internal List<Connection> Connections => _connections.Values.ToList();
        internal LocalDevice Localdevice => _devices.Local;

        private ILogger? _logger;

        public EEBUSManager(Settings settings, ServiceDiscovery? serviceDiscovery = null, ILogger? logger = null)
        {
            _logger = logger;
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

            _cert = CertificateGenerator.GenerateCert(settings.BasePath, settings.Certificate);
            byte[] hash = SHA1.HashData(_cert.GetPublicKey());

            _mDNSClient = new MDNSClient(serviceDiscovery, CanEvaluateShipPairingRequests);
            _mDNSService = new MDNSService(settings.Device.Id, settings.Device.Port, serviceDiscovery);

            LocalDevice localDevice = _devices.GetOrCreateLocal(hash, settings.Device);

            _mDNSService.Run(localDevice, _cts.Token);
            _mDNSClient.Run(_devices);
            _devices.RemoteDeviceFound += OnRemoteDeviceFound;
            _devices.ServerStateChanged += OnServerStateChanged;
            _devices.ClientStateChanged += OnClientStateChanged;

            lpcStateMachine = new LpcLimitStateMachine(localDevice);
            lpcStateMachine.RegisterEventHandler(new LPCEventHandler(this));
            lppStateMachine = new LppLimitStateMachine(localDevice);
            lppStateMachine.RegisterEventHandler(new LPPEventHandler(this));

            monitoringUseCasesEventHandler = new MonitoringUseCasesEventHandler(this);
            notifyEventHandler = new NotifyEventHandler(this);
            deviceConnectionStatusEventHandler = new DeviceConnectionStatusEventHandler(this);
            _devices.Local.AddUseCaseEvents(this.lpcStateMachine);
            _devices.Local.AddUseCaseEvents(this.lppStateMachine);
            _devices.Local.AddUseCaseEvents(this.notifyEventHandler);
            _devices.Local.AddUseCaseEvents(this.monitoringUseCasesEventHandler);
            _devices.Local.AddUseCaseEvents(this.deviceConnectionStatusEventHandler);
            _settings = settings;
            this._serviceDiscovery = serviceDiscovery;

            _ = Task.Run(() => ReconnectLoopAsync(_cts.Token));
        }

        private async Task ReconnectLoopAsync(CancellationToken cancellationToken)
        {
            Thread.CurrentThread.IsBackground = true;
            Debug.WriteLine("[EEBUS] Starting Reconnect Loop");
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(ReconnectLoopIntervalMs, cancellationToken);

                try
                {
                    _logger?.LogDebug("[EEBUS] Running Reconnect Loop. Current connections: " + _connections.Count);
                    foreach (var device in _devices.GetRemotes())
                    {
                        lock (_lock)
                        {
                            if (!_trustedSkis.Contains(device.SKI.ToString())) continue;
                        }

                        Connection? connection = Connections.FirstOrDefault(c => c.Remote != null && c.Remote.SKI == device.SKI);
                        if ((connection == null 
                            || connection.WebSocket.State != WebSocketState.Open 
                            || connection.State == Connection.EState.Disconnected
                            || connection.State == Connection.EState.Stopped
                            || connection.State == Connection.EState.ErrorOrTimeout) 
                            && !cancellationToken.IsCancellationRequested)
                        {
                            _logger?.LogDebug($"Device {device.Name} does not have an active connection anymore, reconnecting...");
                            try
                            {
                                await ConnectAsync(device.SKI.ToString());
                            } catch (Exception)
                            {
                                _logger?.LogError("Unable to connect to device {deviceName}", device.Name);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }

        private bool CanEvaluateShipPairingRequests()
        {
            // Snapshot Paired/Remote under the Devices mutex to avoid racing
            // with GetOrCreatePaired/GetOrCreateRemote mutations elsewhere.
            var pairedSnapshot = _devices.GetPairedDevices();
            var pairedAddCu = pairedSnapshot.FirstOrDefault(p => p.TrustType == EEBUS.Models.ShipTrustType.AddCu);
            if (pairedAddCu == null) return true;  //no device yet paired via ship pairing, so we can accept new requests

            //var remote = Devices.Remote.First(r => r.Id == pairedAddCu.TrustId);   //the remote device corresponding to the paired device
            //TODO: Check if this change is correct!
            var remote = _devices.GetRemote(pairedAddCu.TrustId);   //the remote device corresponding to the paired device
            if (remote == null) return false;

            bool isConnected = Connections.Any(c => c.Remote != null && c.Remote.Id == remote.Id);
            if (isConnected) return false;  //do not process pairing requests when we are still connected to an addCu device

            TimeSpan diff = DateTime.UtcNow - remote.LastDisconnectUtc;
            //if (diff.TotalMinutes > 15)
            //{
            //    //addCuRemote.TrustType = EEBUS.Models.ShipTrustType.SkiVerification;
            //}
            return diff.TotalMinutes > 15;  //if the device was not connected for more than 15 minutes, we can accept new requests
        }

        private static Type[] GetTypesInNamespace(Assembly assembly, string nameSpace)
        {
            return assembly.GetTypes()
                            .Where(t => String.Equals(t.Namespace, nameSpace, StringComparison.Ordinal))
                            .ToArray();
        }

        public void AddTrustedSki(string ski)
        {
            lock (_lock)
            {
                if (!_trustedSkis.Contains(ski))
                {
                    _trustedSkis.Add(ski);
                }
            }
        }

        public void RemoveTrustedSki(string ski)
        {
            lock (_lock)
            {
                _trustedSkis.Remove(ski);
                //TODO: shouldn't we close the connection, too?
            }
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

        public string GetConnectionInfo(string ski)
        {
            Connection? connection = _connections.Values.FirstOrDefault(c => c.Remote?.SKI.ToString() == ski);
            if (connection == null) return "No active connection for ski " + ski;

            string connectionType = connection is Client ? "Client" : "Server";
            string wsState = connection.WebSocket.State.ToString();
            bool isTrusted = false;
            lock(_lock)
            {
                isTrusted = _trustedSkis.Contains(ski);
            }
            string connectionState = connection.State.ToString() + " - " + connection.SubState.ToString();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Connection type: " + connectionType + ";");
            sb.AppendLine("Websocket state: " + wsState + ";");
            sb.AppendLine("Is trusted: " + isTrusted + ";");
            sb.AppendLine("Connection state: " + connectionState + ";");
            return sb.ToString();
        }

        private LpcLimitStateMachine lpcStateMachine;
        private LppLimitStateMachine lppStateMachine;
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
                if (connection.ConnectionStatus == DeviceConnectionStatus.UseCaseDiscoveryCompleted)
                {
                    //if (connection.Remote != null && EEBusManager.Localdevice.SKI > connection.Remote.SKI)  //device with bigger ski shall close old connections according to spec
                    //{
                    //    var existing = EEBusManager.Connections.FirstOrDefault(c => c.Remote != null && c.Remote.SKI == connection.Remote.SKI);
                    //    if (existing?.Remote != null)
                    //    {
                    //        Debug.WriteLine("Found double connection, closing old connection...");
                    //        await EEBusManager.DisconnectAsync(existing.Remote.SKI.ToString());
                    //    }
                    //}
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

            public Task<WriteApprovalResult> ApproveFailsafeDurationMinimumWriteAsync(FailsafeDurationWriteRequest request)
            {
                Console.WriteLine($"Failsafe Duration Write Request: Duration={request.Duration}");
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public async Task OnStateChanged(LimitState oldState, LimitState newState, string reason)
            {
                Console.WriteLine($"OnStateChanged {oldState} -> {newState} ({reason})");
            }

            public async Task OnFailsafeEntered(string reason)
            {
                Console.WriteLine($"Entered Failsafe");
            }

            public async Task OnFailsafeExited(string reason)
            {
                Console.WriteLine($"Left Failsafe");
            }

            public async Task OnEffectiveLimitChanged(EffectiveLimit limit)
            {
                Console.WriteLine("UpdateLimit");

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

            public Task<WriteApprovalResult> ApproveFailsafeDurationMinimumWriteAsync(FailsafeDurationWriteRequest request)
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

            bool lppActive = false;
            long lppLimit = 0;
            TimeSpan lppDuration = new();

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

            var payload = new
            {
                name = local.Name,
                ski = local.SKI.ToReadable(),
                shipId = local.ShipID,

                lpc = lpcStateMachine.GetEffectiveLimit(),
                lpcActive = lpcActive,
                lpcLimit = lpcLimit,
                lpcDuration = lpcDuration,
                lpcFailsafeLimit = local.GetFailsafeLimit(PowerDirection.Consumption),

                lpp = lppStateMachine.GetEffectiveLimit(),
                lppActive = lppActive,
                lppLimit = lppLimit,
                lppDuration = lppDuration,
                lppFailsafeLimit = local.GetFailsafeLimit(PowerDirection.Production),

                failsafeDurationMinimum = local.GetFailsafeDurationMinimum()
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
                else if (data.CharacteristicType == "contractualProductionNominalMax")
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
                UseCaseSupport = local.GetUseCaseSupport(),
                ShipId = local.ShipID
            };
        }

        public DeviceData? GetRemoteData(string ski)
        {
            RemoteDevice? remote = _devices.GetRemotes().FirstOrDefault(r => r.SKI.ToString() == ski);
            if (remote == null) return null;

            var connection = Connections.FirstOrDefault(c => c.Remote != null && c.Remote.SKI.ToString() == ski);
            if (connection == null || connection.WebSocket.State != WebSocketState.Open) return null;    //remote does not have an active connection anymore

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

            var projection = _devices.GetRemotes().Select(rd => new
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
            return _devices.GetRemotes().Select(rd => new RemoteDeviceData
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

            RemoteDevice? device = _devices.GetRemotes().FirstOrDefault(rd => rd.SKI == Ski);
            if (null == device)
            {
                return null;
            }

            Uri uri = new Uri("wss://" + device.Url);
            HostString hostString = new HostString(uri.Host, uri.Port);

            ClientWebSocket? wsClient = null;
            try
            {
                // Any existing connection for this SKI counts as a hit, regardless of
                // host/port (covers the case where we already have a server-side
                // connection on a different URI than the one mDNS now advertises).
                Connection? existingClient = _connections.Values
                    .FirstOrDefault(c => c.Remote != null && c.Remote.SKI == Ski);

                if (existingClient != null)
                {
                    if (existingClient.WebSocket?.State == WebSocketState.Open)
                    {
                        return existingClient.RemoteHost.ToString();
                    }

                    // Stale connection - tear it down before opening a new one.
                    await DisconnectAsync(existingClient.RemoteHost).ConfigureAwait(false);
                }

                wsClient = new ClientWebSocket();
                wsClient.Options.AddSubProtocol("ship");
                wsClient.Options.RemoteCertificateValidationCallback = (object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
                {
                    if (cert == null)
                    {
                        return false;
                    }

                    //strict mode (only ship paired devices can communicate):
                    if (_settings.UseStrictShipPairing)
                    {
                        bool isUntrusted = _devices.GetPairedDevices().FirstOrDefault(p => p.TrustId == device.Id && p.TrustType == EEBUS.Models.ShipTrustType.None) != null;
                        if (isUntrusted) return false;
                    }

                    var paired = _devices.GetPairedDevices().FirstOrDefault(p => p.TrustId == device.Id && p.TrustType == EEBUS.Models.ShipTrustType.AddCu);
                    if (paired == null) //device was never paired via ship pairing -> ski verification
                    {
                        byte[] hash = SHA1.HashData(cert.GetPublicKey() ?? []);
                        var certSki = new SKI(hash);
                        var skiString = certSki.ToString();

                        if (device.SKI.ToString() != skiString)
                        {
                            Console.WriteLine($"Certificate SKI {skiString} does not match device SKI {device.SKI.ToString()}");
                            return false;
                        }
                    }
                    else
                    {
                        /*
                        TODO: we imagine the following scenario:
                        1. devZ pairs via ship pairing
                        2. devZ disconnects for more than 15 minutes
                        3. a new devZ2 pairs via ship pairing
                        4. devZ comes back online and attempts a connection
                        Normally, this would not be possible because according to spec, devZ is not trusted anymore. However, with our system, if the device is still teached in, its also still trusted. We would need
                        to automatically delete/disable/set offline the device. Also for the pairing, according to spec, after the device is paired its considered trusted, which would mean we would need to automatically teach it in
                         */

                        byte[] fpHash = SHA256.HashData(cert.GetRawCertData());
                        string fingerprint = Convert.ToHexString(fpHash);
                        if (fingerprint != paired.TrustPar)
                        {
                            Console.WriteLine($"Certificate fingerprint for id {device.Id} does not match persisted trustPar");
                            return false;
                        }
                    }

                    bool isValid = false;
                    lock (_lock)
                    {
                        isValid = _trustedSkis.Contains(device.SKI.ToString());
                    }
                    return isValid;
                };
                wsClient.Options.ClientCertificates.Add(_cert);

                await wsClient.ConnectAsync(uri, _cts.Token).ConfigureAwait(false);

                if (wsClient.State != WebSocketState.Open)
                {
                    wsClient.Dispose();
                    wsClient = null;
                    await DisconnectAsync(hostString).ConfigureAwait(false);
                    return null;
                }

                Client client = new Client(hostString, wsClient, _devices, device, _logger);
                if (!_connections.TryAdd(hostString, client))
                {
                    // Another path inserted a connection for this host while we were
                    // connecting; drop ours to avoid leaking a duplicate socket.
                    wsClient.Dispose();
                    wsClient = null;
                    return hostString.ToString();
                }

                // Ownership of wsClient is transferred to the Client/Connection.
                wsClient = null;
                await client.Run(_cts.Token).ConfigureAwait(false);
                return hostString.ToString();
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                wsClient?.Dispose();
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Connect error for SKI {Ski}.", ski);
                wsClient?.Dispose();
                throw;
            }
        }

        public async Task DisconnectAsync(HostString host)
        {
            var wsClient = _connections.TryGetValue(host, out Connection? client) ? client?.WebSocket : null;
            if (client == null || wsClient == null)
            {
                _logger?.LogInformation("No connection found for host {Host}.", host);
                return;
            }

            try
            {
                // send close message
                CloseMessage closeMessage = new CloseMessage(ConnectionClosePhaseType.announce);
                closeMessage.connectionClose.reason = ConnectionCloseReasonType.removedConnection;
                await client.PushCloseMessageAsync(closeMessage);

                // now close websocket
                await wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Disconnect error for host {Host}.", host);
            }
            finally
            {
                //We need to dispose the websocket in any case, e.g. it can be that we send a close message, but we do not receive one from the remote partner. In this case, we must close the connection
                if (_connections.TryRemove(host, out Connection? removedClient) && removedClient != null)
                {
                    await removedClient.CloseAsync();
                }

                try
                {
                    wsClient.Dispose();
                }
                catch (Exception)
                {
                }
                wsClient = null;
            }
        }

        /// <summary>
        /// Disconnects the active connection whose remote device has the given SKI.
        /// </summary>
        /// <param name="remoteSki">The SKI of the remote device to disconnect.</param>
        /// <returns>
        /// <c>true</c> if a matching connection was found and disconnect was attempted;
        /// <c>false</c> if no connection currently matches the given SKI.
        /// </returns>
        public async Task<bool> DisconnectAsync(string remoteSki)
        {
            if (string.IsNullOrEmpty(remoteSki))
                return false;

            // Find the matching connection explicitly instead of relying on
            // FirstOrDefault().Key, which would otherwise hand back a default
            // HostString on a miss and silently "disconnect" nothing.
            var match = _connections.FirstOrDefault(c =>
                c.Value?.Remote?.SKI.ToString() == remoteSki);

            if (match.Value == null)
            {
                Debug.WriteLine($"DisconnectAsync: no active connection found for SKI {remoteSki}");
                return false;
            }

            await DisconnectAsync(match.Key).ConfigureAwait(false);
            return true;
        }

        public void Start()
        {
            _shipListener = new SHIPListener(_devices, _settings, _logger);
            _shipListener.OnNewConnectionValidation = (args) =>
            {
                RemoteDevice? device = _devices.GetRemotes().FirstOrDefault(rd => rd.SKI.ToString() == args.Ski);
                if (null == device)
                {
                    Console.WriteLine($"remote device with SKI {args.Ski} has no mDNS advertisements");
                    return false;
                }

                var paired = _devices.GetPairedDevices().FirstOrDefault(p => p.TrustId == device.Id);

                if (paired != null) //device was added via ship pairing
                {
                    byte[] fpHash = SHA256.HashData(args.Certificate.GetRawCertData());
                    string fingerprint = Convert.ToHexString(fpHash);
                    if (fingerprint != paired.TrustPar)
                    {
                        Console.WriteLine($"Certificate fingerprint for id {device.Id} does not match persisted trustPar");
                        return false;
                    }
                }
                else
                {
                    byte[] hash = SHA1.HashData(args.Certificate.GetPublicKey() ?? []);
                    var ski = new SKI(hash);
                    var skiString = ski.ToString();

                    if (device.SKI.ToString() != skiString)
                    {
                        Console.WriteLine($"Certificate SKI {skiString} does not match device SKI {device.SKI.ToString()}");
                        return false;
                    }
                }

                bool isValid = false;
                lock (_lock)
                {
                    isValid = _trustedSkis.Contains(args.Ski);
                }
                return isValid;
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

        public async Task StopAsync()
        {
            // Stop the listener first so no new connections come in while we tear down.
            if (_shipListener != null)
            {
                try
                {
                    await _shipListener.StopAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("EEBUSManager.StopAsync: SHIPListener stop failed: " + ex.Message);
                }
            }

            _mDNSClient.Stop();

            // Best-effort disconnect of every live connection. Snapshot first so we
            // don't enumerate _connections while DisconnectAsync mutates it.
            HostString[] hosts = _connections.Keys.ToArray();
            foreach (var host in hosts)
            {
                try
                {
                    await DisconnectAsync(host).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("EEBUSManager.StopAsync: disconnect failed for " + host + ": " + ex.Message);
                }
            }
        }

        public void Dispose()
        {
            // Cancel first so background loops (ReconnectLoopAsync, mDNS) start unwinding
            // before we wait on graceful shutdown.
            try { _cts.Cancel(); } catch (ObjectDisposedException) { }

            try
            {
                StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("EEBUSManager.Dispose: StopAsync failed: " + ex.Message);
            }

            _devices.RemoteDeviceFound -= OnRemoteDeviceFound;
            _devices.ServerStateChanged -= OnServerStateChanged;
            _devices.ClientStateChanged -= OnClientStateChanged;

            _devices.Local.RemoveUseCaseEvents(this.lpcStateMachine);
            _devices.Local.RemoveUseCaseEvents(this.lppStateMachine);
            _devices.Local.RemoveUseCaseEvents(this.notifyEventHandler);
            _devices.Local.RemoveUseCaseEvents(this.monitoringUseCasesEventHandler);
            _devices.Local.RemoveUseCaseEvents(this.deviceConnectionStatusEventHandler);

            lpcStateMachine.Dispose();
            lppStateMachine.Dispose();

            if (_serviceDiscoveryNeedsDispose)
            {
                _serviceDiscovery?.Dispose();
            }

            try { _cert?.Dispose(); } catch { }
            try { _cts.Dispose(); } catch { }
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
