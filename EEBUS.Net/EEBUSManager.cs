using EEBUS.DataStructures;
using EEBUS.Enums;
using EEBUS.Features;
using EEBUS.KeyValues;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net.EEBUS;
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
using System.Diagnostics;
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
        private const int ReconnectLoopIntervalMs = 10000;
        private const int DoubleConnectionTimeoutMs = 3000;   //SHIP 12.2.3: time the node with the smaller SKI waits for the bigger one to resolve a double connection

        #region Handlers
        private class MessageEventHandler(EEBUSManager EEBusManager) : MessageEvents
        {
            public Task<bool> BeforeMessageSentAsync(ShipMessageBase message, Connection connection)
                => EEBusManager.BeforeMessageSent?.Invoke(message, connection) ?? Task.FromResult(true);
        }

        private class NotifyEventHandler(EEBUSManager EEBusManager) : NotifyEvents
        {
            /// <summary>
            /// Notifies all subscribers of the local feature with the provided <paramref name="payload"/>. The notification is sent to all clients that have subscribed to the feature identified by <paramref name="localFeatureAddress"/>.
            /// </summary>
            /// <param name="payload"></param>
            /// <param name="localFeatureAddress"></param>
            /// <returns></returns>
            /// <exception cref="Exception"></exception>
            public Task NotifyAsync(JsonNode? payload, AddressType localFeatureAddress)
            {
                if (payload == null) return Task.CompletedTask;
                AddressType serverAddress = localFeatureAddress;

                var connections = EEBusManager._connections.Values.ToList();

                //We look for all subscribers across all connections and broadcast the notification to all of them
                foreach (Connection connection in connections)
                {
                    IEnumerable<AddressType> clientAddresses = connection.BindingAndSubscriptionManager.GetSubscriptionsByServerAddress(serverAddress, BindingSubscriptionDirection.Incoming);

                    foreach (var clientAddress in clientAddresses)
                    {
                        SpineDatagramPayload reply = new SpineDatagramPayload();
                        reply.datagram.header.addressSource = serverAddress;
                        reply.datagram.header.addressDestination = clientAddress;
                        reply.datagram.header.msgCounter = DataMessage.NextCount;
                        reply.datagram.header.cmdClassifier = "notify";

                        reply.datagram.payload = payload;
                        DataMessage dataMessage = new DataMessage();
                        dataMessage.SetPayload(JsonHelper.ToJsonNode(reply) ?? throw new Exception("Failed to serialize data message"));
                        connection.PushDataMessage(dataMessage);
                    }
                }
                return Task.CompletedTask;
            }
        }

        private class DeviceConnectionStatusEventHandler(EEBUSManager EEBusManager) : DeviceConnectionStatusEvents
        {
            /// <summary>
            /// Handles connection status updates for a connection
            /// </summary>
            /// <param name="connection"></param>
            /// <returns></returns>
            public Task DeviceConnectionStatusUpdatedAsync(Connection connection)
            {
                //Usually, we have this flow: Unknown -> NodeDiscoveryCompleted -> UseCaseDiscoveryCompleted. When the use case discovery is completed, we assume the device is fully connected

                EEBusManager._logger?.LogDebug("Status Changed for device {ski}: {status}", connection.Remote?.SKI.ToString(), connection.ConnectionStatus.ToString());
                if (connection.ConnectionStatus == DeviceConnectionStatus.UseCaseDiscoveryCompleted)
                {
                    //TODO: this code does not work properly, but it is correct that the spec describes this mechanism
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

                if (connection.ConnectionStatus == DeviceConnectionStatus.NodeDiscoveryCompleted)
                {
                    connection.SendUseCaseDiscoveryRead();  //We only send the use case discovery read after the node discovery is completed, as we need the node information first (entities, features)
                }

                return Task.CompletedTask;
            }
        }

        private class MonitoringUseCasesEventHandler(EEBUSManager eebusManager) : MonitoringUseCaseEvents
        {
            public async Task DataUpdateMeasurementsAsync(List<MeasurementData.MeasurementData> measurementData, string ski)
            {
                await eebusManager.RaiseDeviceDataChangedAsync(new DeviceData
                {
                    SKI = ski,
                    Measurements = measurementData.CollectData()
                });
            }

            public async Task DeviceConfigurationChangedAsync(Device device)
            {
                KeyValue? pvCurtailmentLimitFactor = device.KeyValues.FirstOrDefault(kv => kv.KeyName == "pvCurtailmentLimitFactor");
                MgcpData? mgcpData = pvCurtailmentLimitFactor == null ? null : new MgcpData
                {
                    PvCurtailmentLimitFactor = pvCurtailmentLimitFactor.Data.value.scaledNumber?.ToLong()
                };

                await eebusManager.RaiseDeviceDataChangedAsync(new DeviceData
                {
                    SKI = device.SKI.ToString(),
                    Mgcp = mgcpData
                });
            }
        }

        private class LPCEventHandler(EEBUSManager EEBusManager) : ILimitStateMachineEvents
        {
            public Task<WriteApprovalResult> ApproveActiveLimitWriteAsync(ActiveLimitWriteRequest request)
            {
                EEBusManager._logger?.LogDebug($"LPC Active Limit Write Request: Value={request.Value}, Active={request.IsLimitActive}");
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public Task<WriteApprovalResult> ApproveFailsafeLimitWriteAsync(FailsafeLimitWriteRequest request)
            {
                EEBusManager._logger?.LogDebug($"LPC Failsafe Limit Write Request: Value={ScaledNumber.ToLong(request.Value, request.Scale)}");
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public Task<WriteApprovalResult> ApproveFailsafeDurationMinimumWriteAsync(FailsafeDurationWriteRequest request)
            {
                EEBusManager._logger?.LogDebug($"Failsafe Duration Write Request: Duration={request.Duration}");
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public async Task OnStateChanged(LimitState oldState, LimitState newState, string reason)
            {
                EEBusManager._logger?.LogDebug($"OnStateChanged {oldState} -> {newState} ({reason})");
                await EEBusManager.RaiseDeviceDataChangedAsync(new DeviceData { Lpc = new LpcLppData { LimitState = newState } });
            }

            public Task OnFailsafeEntered(string reason)
            {
                EEBusManager._logger?.LogDebug("Entered Failsafe");
                return Task.CompletedTask;
            }

            public Task OnFailsafeExited(string reason)
            {
                EEBusManager._logger?.LogDebug("Left Failsafe");
                return Task.CompletedTask;
            }

            public async Task OnFailsafeLimitChanged(long oldLimit, long newLimit)
            {
                EEBusManager._logger?.LogDebug($"LPC Failsafe Limit changed {oldLimit} -> {newLimit}");
                await EEBusManager.RaiseDeviceDataChangedAsync(new DeviceData { Lpc = new LpcLppData { FailSafeLimit = newLimit } });
            }

            public async Task OnEffectiveLimitChanged(EffectiveLimit limit)
            {
                EEBusManager._logger?.LogDebug("UpdateLimit");
                await EEBusManager.RaiseDeviceDataChangedAsync(new DeviceData
                {
                    Lpc = new LpcLppData
                    {
                        LimitActive = limit.IsLimited,
                        Limit = limit.Value,
                        LimitDuration = (int?)limit.ExpiresAt?.Subtract(DateTimeOffset.Now).Duration().TotalSeconds
                    }
                });
            }
        }

        private class LPPEventHandler(EEBUSManager EEBusManager) : ILimitStateMachineEvents
        {
            public Task<WriteApprovalResult> ApproveActiveLimitWriteAsync(ActiveLimitWriteRequest request)
            {
                EEBusManager._logger?.LogDebug($"LPP Active Limit Write Request: Value={request.Value}, Active={request.IsLimitActive}");
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public Task<WriteApprovalResult> ApproveFailsafeLimitWriteAsync(FailsafeLimitWriteRequest request)
            {
                EEBusManager._logger?.LogDebug($"LPP Failsafe Limit Write Request: Value={ScaledNumber.ToLong(request.Value, request.Scale)}");
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public Task<WriteApprovalResult> ApproveFailsafeDurationMinimumWriteAsync(FailsafeDurationWriteRequest request)
            {
                EEBusManager._logger?.LogDebug($"Failsafe Duration Write Request: Duration={request.Duration}");
                return Task.FromResult(WriteApprovalResult.Accept());
            }

            public Task OnEffectiveLimitChanged(EffectiveLimit limit) => Task.CompletedTask;

            public async Task OnFailsafeLimitChanged(long oldLimit, long newLimit)
            {
                EEBusManager._logger?.LogDebug($"LPP Failsafe Limit changed {oldLimit} -> {newLimit}");
                await EEBusManager.RaiseDeviceDataChangedAsync(new DeviceData { Lpp = new LpcLppData { FailSafeLimit = newLimit } });
            }

            public Task OnStateChanged(LimitState oldState, LimitState newState, string reason)
                => EEBusManager.RaiseDeviceDataChangedAsync(new DeviceData { Lpp = new LpcLppData { LimitState = newState } });
        }

        private LpcLimitStateMachine lpcStateMachine;
        private LppLimitStateMachine lppStateMachine;
        private MonitoringUseCasesEventHandler monitoringUseCasesEventHandler;
        private NotifyEventHandler notifyEventHandler;
        private DeviceConnectionStatusEventHandler deviceConnectionStatusEventHandler;
        private MessageEventHandler _messageEventHandler;
        #endregion

        #region Public events
        public event EventHandler<RemoteDevice>? OnDeviceFound;
        public Func<LimitDataChangedEventArgs, Task>? OnLimitDataChanged;
        public Func<DeviceData, Task>? OnDeviceDataChanged { get; set; }
        public Func<RemoteDevice, DeviceConnectionStatus, Task>? OnDeviceConnectionStatusChanged { get; set; }
        #endregion

        private Task RaiseDeviceDataChangedAsync(DeviceData deviceData)
            => OnDeviceDataChanged?.Invoke(deviceData) ?? Task.CompletedTask;

        private Task RaiseDeviceConnectionStatusChangedAsync(RemoteDevice? remote, DeviceConnectionStatus status)
            => remote == null ? Task.CompletedTask : OnDeviceConnectionStatusChanged?.Invoke(remote, status) ?? Task.CompletedTask;

        #region Testing events
        internal Func<ShipMessageBase, Connection, Task<bool>>? BeforeMessageSent { get; set; }
        internal event EventHandler<StateChangedEventArgs>? ClientStateChanged;
        internal event EventHandler<StateChangedEventArgs>? ServerStateChanged;
        #endregion

        private readonly MDNSClient _mDNSClient;
        private readonly MDNSService _mDNSService;
        private SHIPListener? _shipListener;
        private readonly ServiceDiscovery? _serviceDiscovery;
        private bool _serviceDiscoveryNeedsDispose = false;

        private ConcurrentDictionary<string, Connection> _connections = new();
        //SHIP 12.2.3: older connection to a SKI that is held while a double connection is being resolved by the peer (we have the smaller SKI)
        private ConcurrentDictionary<string, Connection> _duplicateConnections = new();
        private List<string> _trustedSkis = new();
        private object _lock = new object();

        private Devices _devices;
        private readonly Settings _settings;
        private CancellationTokenSource _cts = new();
        private X509Certificate2 _cert;
        public Devices Devices => _devices;
        private ILogger? _logger;
        private const int StatusLoopIntervalMs = 2000;

        //Only touched by the status loop. Contains only SKIs whose last published status was not Unknown
        private readonly ConcurrentDictionary<string, (DeviceConnectionStatus Status, RemoteDevice? Remote)> _lastPublished = new();

        public EEBUSManager(Settings settings, ServiceDiscovery? serviceDiscovery = null, ILogger? logger = null)
        {
            _logger = logger;
            if (serviceDiscovery == null)
            {
                _serviceDiscoveryNeedsDispose = true;
                serviceDiscovery = EEBusServiceDiscovery.Create(settings.FilterLinkLocalAddresses);
            }

            //Run the static constructors for all classes in the EEBUS namespaces to ensure that the static registration of data structures, features, key values, and use cases is executed.
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

            //Fingerprint is used for ship pairing
            byte[] fpHash = SHA256.HashData(_cert.GetRawCertData());
            string fingerprint = Convert.ToHexString(fpHash);

            //Every ship node advertises its own ship service instance and listens to incoming ship service instances
            _mDNSClient = new MDNSClient(serviceDiscovery, CanEvaluateShipPairingRequests, logger);
            _mDNSService = new MDNSService(settings.Device.Id, settings.Device.Port, serviceDiscovery, settings.FilterLinkLocalAddresses);

            LocalDevice localDevice = _devices.GetOrCreateLocal(hash, settings.Device, fingerprint);


            _devices.RemoteDeviceFound += OnRemoteDeviceFound;
            _devices.ServerStateChanged += OnServerStateChanged;
            _devices.ClientStateChanged += OnClientStateChanged;


            //Register event handlers. The events can be triggered by calling localDevice.GetUseCaseEvents()
            lpcStateMachine = new LpcLimitStateMachine(localDevice, logger);
            lpcStateMachine.RegisterEventHandler(new LPCEventHandler(this));
            lppStateMachine = new LppLimitStateMachine(localDevice);
            lppStateMachine.RegisterEventHandler(new LPPEventHandler(this));

            monitoringUseCasesEventHandler = new MonitoringUseCasesEventHandler(this);
            notifyEventHandler = new NotifyEventHandler(this);
            deviceConnectionStatusEventHandler = new DeviceConnectionStatusEventHandler(this);
            _messageEventHandler = new MessageEventHandler(this);
            _devices.Local.AddUseCaseEvents(this.lpcStateMachine);
            _devices.Local.AddUseCaseEvents(this.lppStateMachine);
            _devices.Local.AddUseCaseEvents(this.notifyEventHandler);
            _devices.Local.AddUseCaseEvents(this.monitoringUseCasesEventHandler);
            _devices.Local.AddUseCaseEvents(this.deviceConnectionStatusEventHandler);
            _devices.Local.AddUseCaseEvents(this._messageEventHandler);
            _settings = settings;
            this._serviceDiscovery = serviceDiscovery;

            if (settings.EnablePeriodicReconnect)
            {
                _ = Task.Run(() => ReconnectLoopAsync(_cts.Token));
            }
            _ = Task.Run(() => DeviceConnectionStatusLoop(_cts.Token));
        }

        /// <summary>
        /// Periodically checks for stale connections and attempts reconnection if possible
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ReconnectLoopAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine("[EEBUS] Starting Reconnect Loop");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(ReconnectLoopIntervalMs, cancellationToken);

                    _logger?.LogDebug("[EEBUS] Running Reconnect Loop. Current connections: " + _connections.Count);
                    foreach (var device in _devices.GetRemotes())
                    {
                        string ski = device.SKI.ToString();
                        if (cancellationToken.IsCancellationRequested || !IsTrusted(ski) || IsConnectionAlive(GetConnection(ski)))
                            continue;

                        //TryConnectAsync handles and logs all errors itself
                        _logger?.LogDebug("Device {deviceName} does not have an active connection anymore, reconnecting...", device.Name);
                        await TryConnectAsync(ski);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in reconnect loop");
                }
            }
        }

        private async Task DeviceConnectionStatusLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(StatusLoopIntervalMs, cancellationToken).ConfigureAwait(false);
                    await PublishStatusChangesAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in status loop");
                }
            }
        }

        /// <summary>
        /// Raises OnDeviceConnectionStatusChanged for every SKI whose status changed since the last run.
        /// Single caller, so events are serialized and always reflect the current state.
        /// </summary>
        private async Task PublishStatusChangesAsync()
        {
            //_connections: currently connected devices; _lastPublished: devices last reported as online (may have disconnected since)
            List<string> connections = _connections.Keys.Union(_lastPublished.Keys).ToList();
            foreach (string ski in connections)
            {
                DeviceConnectionStatus status = GetConnectionStatus(ski);
                bool wasPublished = _lastPublished.TryGetValue(ski, out var last);
                DeviceConnectionStatus lastStatus = wasPublished ? last.Status : DeviceConnectionStatus.Unknown;

                if (status == lastStatus)   //No status change since last run -> nothing to do
                    continue;

                RemoteDevice? remote;
                if (status == DeviceConnectionStatus.Unknown)
                {
                    //Connection is gone, use the remote stored when it was last online
                    remote = last.Remote;
                    _lastPublished.TryRemove(ski, out _);
                }
                else
                {
                    remote = GetConnection(ski)?.Remote ?? last.Remote;
                    _lastPublished[ski] = (status, remote);
                }

                try
                {
                    await RaiseDeviceConnectionStatusChangedAsync(remote, status).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in OnDeviceConnectionStatusChanged handler for device {ski} ({status})", ski, status);
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

            var remote = _devices.GetRemote(pairedAddCu.TrustId);   //the remote device corresponding to the paired device
            if (remote == null) return false;

            if (IsConnectionAlive(GetConnection(remote.SKI.ToString()))) return false;
            if (remote.LastDisconnectUtc == null) return false;

            TimeSpan diff = DateTime.UtcNow - remote.LastDisconnectUtc.Value;
            return diff.TotalMinutes > 15;  //if the device was not connected for more than 15 minutes, we can accept new requests
        }
        private static Type[] GetTypesInNamespace(Assembly assembly, string nameSpace)
        {
            return assembly.GetTypes()
                            .Where(t => String.Equals(t.Namespace, nameSpace, StringComparison.Ordinal))
                            .ToArray();
        }
        private void OnRemoteDeviceFound(RemoteDevice device)
        {
            _logger?.LogInformation("Remote device found: {deviceName} ({deviceSki})", device.Name, device.SKI.ToString());
            OnDeviceFound?.Invoke(this, device);
        }
        /// <summary>
        /// Opens a client connection to the remote device with the given <paramref name="ski"/> unless a live connection exists already.
        /// Returns true once the connection has been registered and its receive loop has ended.
        /// </summary>
        public async Task<bool> TryConnectAsync(string ski)
        {
            SKI remoteSki = new SKI(ski);
            RemoteDevice? device = _devices.GetRemotes().FirstOrDefault(rd => rd.SKI == remoteSki);
            if (device == null || IsConnectionAlive(GetConnection(ski)))
                return false;

            Uri uri = new Uri("wss://" + device.Url);
            ClientWebSocket? wsClient = null;
            try
            {
                wsClient = new ClientWebSocket();
                wsClient.Options.AddSubProtocol("ship");
                wsClient.Options.KeepAliveInterval = TimeSpan.FromMilliseconds(SHIPMessageTimeout.WS_KEEP_ALIVE_INTERVAL);
                wsClient.Options.KeepAliveTimeout = TimeSpan.FromMilliseconds(SHIPMessageTimeout.WS_KEEP_ALIVE_TIMEOUT);
                wsClient.Options.RemoteCertificateValidationCallback = (_, cert, _, _) => ValidateServerCertificate(device, cert);
                wsClient.Options.ClientCertificates.Add(_cert);

                //ConnectAsync either completes with the socket in the Open state or throws; failures are handled in the catch blocks below.
                await wsClient.ConnectAsync(uri, _cts.Token).ConfigureAwait(false);

                Client client = new Client(new HostString(uri.Host, uri.Port), wsClient, _devices, device, _logger);
                wsClient = null;    //Ownership of the websocket is transferred to the Client/Connection
                if (!await RegisterConnectionAsync(client).ConfigureAwait(false))
                    return false;

                _logger?.LogDebug("Added client with ski " + ski + " to connections.");
                await client.Run(_cts.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Connect error for SKI {Ski}.", ski);
                return false;
            }
            finally
            {
                wsClient?.Dispose();
            }
        }
        /// <summary>
        /// Validates the certificate presented by a remote device we connect to as a client.
        /// </summary>
        private bool ValidateServerCertificate(RemoteDevice device, X509Certificate? cert)
        {
            if (cert == null)
                return false;

            var pairedDevices = _devices.GetPairedDevices();

            //strict mode (only ship paired devices can communicate):
            if (_settings.UseStrictShipPairing && pairedDevices.Any(p => p.TrustId == device.Id && p.TrustType == EEBUS.Models.ShipTrustType.None))
                return false;

            var paired = pairedDevices.FirstOrDefault(p => p.TrustId == device.Id && p.TrustType == EEBUS.Models.ShipTrustType.AddCu);
            if (paired == null) //device was never paired via ship pairing -> ski verification
            {
                if (!CertificateMatchesSki(device, cert))
                    return false;
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
                if (!CertificateMatchesFingerprint(device, cert, paired.TrustPar))
                    return false;
            }

            return IsTrusted(device.SKI.ToString());
        }
        private bool CertificateMatchesSki(RemoteDevice device, X509Certificate cert)
        {
            string certSki = new SKI(SHA1.HashData(cert.GetPublicKey() ?? [])).ToString();
            string deviceSki = device.SKI.ToString();
            if (certSki == deviceSki)
                return true;

            _logger?.LogError("Certificate SKI {certSki} does not match device SKI {deviceSki}", certSki, deviceSki);
            return false;
        }
        private bool CertificateMatchesFingerprint(RemoteDevice device, X509Certificate cert, string? trustPar)
        {
            if (Convert.ToHexString(SHA256.HashData(cert.GetRawCertData())) == trustPar)
                return true;

            _logger?.LogError("Certificate fingerprint for id {deviceId} does not match persisted trustPar", device.Id);
            return false;
        }
        private bool IsTrusted(string ski)
        {
            lock (_lock)
            {
                return _trustedSkis.Contains(ski);
            }
        }
        /// <summary>
        /// Sends a SHIP close message (if the connection is in the connected state), closes and disposes the websocket and
        /// optionally raises <see cref="OnDeviceConnectionStatusChanged"/>. The connection must already have been removed from the bookkeeping.
        /// </summary>
        private async Task CloseConnectionAsync(Connection connection, ConnectionCloseReasonType reason = ConnectionCloseReasonType.unspecific)
        {
            string ski = connection.Remote?.SKI.ToString() ?? string.Empty;
            WebSocket? ws = connection.WebSocket;
            _logger?.LogDebug("Closing connection with ski " + ski);
            try
            {
                if (ws != null && ws.State == WebSocketState.Open)
                {
                    if (connection.State == Connection.EState.Connected)
                    {
                        // send close message
                        CloseMessage closeMessage = new CloseMessage(ConnectionClosePhaseType.announce);
                        closeMessage.connectionClose.reason = reason;
                        await connection.PushCloseMessageAsync(closeMessage);
                    }

                    // now close websocket
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Disconnect error for connection with ski {Ski}.", ski);
            }
            finally
            {
                //We need to dispose the websocket in any case, e.g. it can be that we send a close message, but we do not receive one from the remote partner. In this case, we must close the connection
                await connection.CloseAsync();

                try
                {
                    ws?.Dispose();
                }
                catch (Exception)
                {
                }
            }
        }
        /// <summary>
        /// Registers <paramref name="newConnection"/> as the connection for <paramref name="ski"/> and resolves double connections according to SHIP 12.2.3:
        /// the node with the bigger SKI keeps only the most recent connection and closes all others. The node with the smaller SKI keeps both
        /// connections for up to <see cref="DoubleConnectionTimeoutMs"/> and lets the peer decide; only if the peer does not act, the older connection is closed.
        /// Returns false if <paramref name="newConnection"/> was closed instead of being registered.
        /// </summary>
        private async Task<bool> RegisterConnectionAsync(Connection newConnection)
        {
            if (newConnection.Remote == null) return false;

            //The connection reports its own end (client and server alike), which removes it from the bookkeeping again
            newConnection.Closed = OnConnectionClosedAsync;

            string remoteSki = newConnection.Remote.SKI.ToString();
            while (!_connections.TryAdd(remoteSki, newConnection))
            {
                //Connection exists already, so we need to decide what to do with it

                if (GetConnection(remoteSki) is not Connection existing)
                    continue;

                if (!IsConnectionAlive(existing) || _devices.Local.SKI > newConnection.Remote.SKI)
                {
                    //Stale connection, or we have the bigger SKI: keep the most recent connection and close the old one (SHIP 12.2.3)
                    if (!_connections.TryRemove(new KeyValuePair<string, Connection>(remoteSki, existing)))
                        continue;   //connection was already removed in the meantime, return to the loop and try to add the new connection again

                    string reason = IsConnectionAlive(existing) ? "local SKI is bigger" : "existing connection is stale";
                    _logger?.LogDebug("Double connection with remote device " + newConnection.Remote.Name + " detected, closing connection with ski " + remoteSki + " (" + reason + ")");
                    await CloseConnectionAsync(existing).ConfigureAwait(false);
                    continue;
                }

                //We have the smaller SKI: the peer decides which connection survives. Keep the older connection in the side table
                //and wait for the peer to close it; if it does not do so within 3 seconds, we close it ourselves.
                if (!_connections.TryUpdate(remoteSki, newConnection, existing))  //replace existing connection with the new one
                    continue;

                if (_duplicateConnections.TryRemove(remoteSki, out Connection? previousDuplicate) && previousDuplicate != null)
                {
                    //more than two connections to the same node - close the oldest one, the peer should have dealt with it already
                    await CloseConnectionAsync(previousDuplicate).ConfigureAwait(false);
                }
                _duplicateConnections[remoteSki] = existing;

                _logger?.LogDebug("Double connection with remote device " + newConnection.Remote.Name + " detected, waiting for peer to close one of them (remote SKI is bigger)");
                _ = Task.Run(() => ResolveDoubleConnectionAfterTimeoutAsync(remoteSki, _cts.Token));
                break;
            }

            return true;
        }
        /// <summary>
        /// SHIP 12.2.3: after the timeout, the node with the smaller SKI pings both connections, closes connections that are not pingable anymore
        /// and MAY close the older one if both are still alive.
        /// </summary>
        private async Task ResolveDoubleConnectionAfterTimeoutAsync(string ski, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(DoubleConnectionTimeoutMs, cancellationToken).ConfigureAwait(false);
                //TODO: do we really need this?
                //await WaitForPingResultAsync(ski, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                if (!_duplicateConnections.TryRemove(ski, out Connection? older) || older == null)
                    return; //already resolved

                if (!IsConnectionAlive(older))
                {
                    //The peer closed the older connection as expected, or it is not pingable anymore
                    await CloseConnectionAsync(older).ConfigureAwait(false);
                    return;
                }

                if (!IsConnectionAlive(GetConnection(ski)))
                {
                    //The peer kept the older connection instead (or the newer one is not pingable anymore); make the older one the active one again
                    await TryPromoteDuplicateAsync(ski).ConfigureAwait(false);
                    return;
                }

                //Both connections are still pingable: the peer did not resolve the double connection within the timeout -> close the older one
                _logger?.LogDebug("Peer did not resolve double connection to ski {ski} within {timeout} ms, closing older connection", ski, DoubleConnectionTimeoutMs);
                await CloseConnectionAsync(older).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error resolving double connection for ski {Ski}.", ski);
            }
        }
        ///// <summary>
        ///// SHIP 12.2.3 requires a WebSocket ping after the double connection timeout. The managed WebSocket cannot send pings on demand, so the sockets
        ///// are configured with keep-alive pings (<see cref="SHIPMessageTimeout.WS_KEEP_ALIVE_INTERVAL"/>/<see cref="SHIPMessageTimeout.WS_KEEP_ALIVE_TIMEOUT"/>)
        ///// and this method waits long enough for at least one full ping/pong round on both connections. A socket that misses its pong is aborted by the
        ///// WebSocket implementation and therefore no longer reported alive. Returns early as soon as one of the connections is no longer alive or the
        ///// double connection has been resolved by someone else.
        ///// </summary>
        //private async Task WaitForPingResultAsync(string ski, CancellationToken cancellationToken)
        //{
        //    const int pollIntervalMs = 250;
        //    int remainingMs = SHIPMessageTimeout.WS_KEEP_ALIVE_INTERVAL + SHIPMessageTimeout.WS_KEEP_ALIVE_TIMEOUT;

        //    while (remainingMs > 0)
        //    {
        //        if (!_duplicateConnections.TryGetValue(ski, out Connection? older) || !IsConnectionAlive(older))
        //            return;
        //        if (!IsConnectionAlive(GetConnection(ski)))
        //            return;

        //        await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
        //        remainingMs -= pollIntervalMs;
        //    }
        //}
        /// <summary>
        /// If a live connection is parked in the duplicate side table for <paramref name="ski"/>, promotes it to the active connection
        /// (replacing a dead active connection, if any). Returns true if a connection was promoted. A dead duplicate is closed and discarded.
        /// </summary>
        private async Task<bool> TryPromoteDuplicateAsync(string ski)
        {
            if (!_duplicateConnections.TryRemove(ski, out Connection? older) || older == null)
                return false;

            if (!IsConnectionAlive(older))
            {
                await CloseConnectionAsync(older).ConfigureAwait(false);
                return false;
            }

            if (GetConnection(ski) is Connection current && !IsConnectionAlive(current)
                && _connections.TryRemove(new KeyValuePair<string, Connection>(ski, current)))
            {
                await CloseConnectionAsync(current).ConfigureAwait(false);
            }

            if (!_connections.TryAdd(ski, older))
            {
                await CloseConnectionAsync(older).ConfigureAwait(false);
                return false;
            }

            _logger?.LogDebug("Peer kept the older connection to ski {ski}, promoting it", ski);
            return true;
        }



        ///// <summary>
        ///// Makes <paramref name="older"/> (already removed from the duplicate side table and alive) the active connection for <paramref name="ski"/>.
        ///// A dead active connection is removed and closed first. If a live connection is registered in the meantime, <paramref name="older"/> is closed instead.
        ///// After promotion the last known status of the promoted connection is re-announced, since a concurrently handled disconnect of the
        ///// replaced connection may already have reported <see cref="DeviceConnectionStatus.Unknown"/>.
        ///// </summary>
        //private async Task<bool> PromoteDuplicateAsync(string ski, Connection older)
        //{
        //    if (GetConnection(ski) is Connection current && !IsConnectionAlive(current)
        //        && _connections.TryRemove(new KeyValuePair<string, Connection>(ski, current)))
        //    {
        //        await CloseConnectionAsync(current).ConfigureAwait(false);
        //    }

        //    if (!_connections.TryAdd(ski, older))
        //    {
        //        await CloseConnectionAsync(older).ConfigureAwait(false);
        //        return false;
        //    }

        //    _logger?.LogDebug("Peer kept the older connection to ski {ski}, promoting it", ski);
        //    await RaiseDeviceConnectionStatusChangedAsync(older.Remote, older.ConnectionStatus);
        //    return true;
        //}
        private static bool IsConnectionAlive(Connection? connection)
        {
            return connection != null
                && connection.WebSocket?.State == WebSocketState.Open
                && connection.State != Connection.EState.Disconnected
                && connection.State != Connection.EState.Stopped
                && connection.State != Connection.EState.ErrorOrTimeout;
        }

        private async Task OnConnectionIncomingAsync(Server server)
        {
            if (server.Remote == null) return;

            if (await RegisterConnectionAsync(server).ConfigureAwait(false))
            {
                _logger?.LogDebug("Added connection with ski " + server.Remote.SKI + " to connections.");
            }
        }
        /// <summary>
        /// Called by every registered connection (client or server) once its receive loop has ended. Removes it from the bookkeeping,
        /// resolves a pending double connection (SHIP 12.2.3) and reports the device as disconnected.
        /// </summary>
        private async Task OnConnectionClosedAsync(Connection connection)
        {
            if (connection.Remote == null) return;

            string ski = connection.Remote.SKI.ToString();
            //Only remove the entry if it still refers to this connection; it may have been replaced by another connection in the meantime
            if (_connections.TryRemove(new KeyValuePair<string, Connection>(ski, connection)))
            {
                //If the peer (bigger SKI) closed the newer connection and kept the older one, promote the older one
                await TryPromoteDuplicateAsync(ski).ConfigureAwait(false);
            }
            else
            {
                _duplicateConnections.TryRemove(new KeyValuePair<string, Connection>(ski, connection));
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
            _devices.Local.RemoveUseCaseEvents(_messageEventHandler);

            lpcStateMachine.Dispose();
            lppStateMachine.Dispose();

            if (_serviceDiscoveryNeedsDispose)
            {
                _serviceDiscovery?.Dispose();
            }

            try { _cert?.Dispose(); } catch { }
            try { _cts.Dispose(); } catch { }
        }

        #region Public methods
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
            }
        }

        public DeviceConnectionStatus GetConnectionStatus(string ski)
        {
            Connection? connection = GetConnection(ski);
            return IsConnectionAlive(connection) ? connection!.ConnectionStatus : DeviceConnectionStatus.Unknown;
        }

        public string GetConnectionInfo(string ski)
        {
            Connection? connection = GetConnection(ski);
            if (connection == null) return "No active connection for ski " + ski;

            string connectionType = connection is Client ? "Client" : "Server";
            string wsState = connection.WebSocket.State.ToString();
            bool isTrusted = IsTrusted(ski);
            string connectionState = connection.State.ToString() + " - " + connection.SubState.ToString();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Connection type: " + connectionType + ";");
            sb.AppendLine("Websocket state: " + wsState + ";");
            sb.AppendLine("Is trusted: " + isTrusted + ";");
            sb.AppendLine("Connection state: " + connectionState + ";");
            return sb.ToString();
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

            EffectiveLimit lpcEffectiveLimit = lpcStateMachine.GetEffectiveLimit();
            EffectiveLimit lppEffectiveLimit = lppStateMachine.GetEffectiveLimit();
            foreach (LoadControlLimitDataStructure data in local.GetDataStructures<LoadControlLimitDataStructure>())
            {
                if (data.LimitDirection == "consume")
                {
                    lpcActive = data.LimitActive;
                    lpcLimit = lpcEffectiveLimit.Value; //use the effective limit here, not the limit from the data structure (if we are in failsafe mode, we want the failsafe limit to be reported here)
                    lpcDuration = data.EndTime == null ? Timeout.InfiniteTimeSpan : XmlConvert.ToTimeSpan(data.EndTime);
                }
                else if (data.LimitDirection == "produce")
                {
                    lppActive = data.LimitActive;
                    lppLimit = lppEffectiveLimit.Value; //use the effective limit here, not the limit from the data structure (if we are in failsafe mode, we want the failsafe limit to be reported here)
                    lppDuration = data.EndTime == null ? Timeout.InfiniteTimeSpan : XmlConvert.ToTimeSpan(data.EndTime);
                }
            }

            foreach (ElectricalConnectionCharacteristicDataStructure data in local.GetDataStructures<ElectricalConnectionCharacteristicDataStructure>())
            {
                if (data.CharacteristicType == "contractualConsumptionNominalMax")
                {
                    lpcContractualNominalMax = ScaledNumber.ToLong(data.Number, data.Scale);
                }
                else if (data.CharacteristicType == "contractualProductionNominalMax")
                {
                    lppContractualNominalMax = ScaledNumber.ToLong(data.Number, data.Scale);
                }
            }

            FailsafeConsumptionActivePowerLimitKeyValue? lpcFailsafeLimitKeyValue = local.GetKeyValue<FailsafeConsumptionActivePowerLimitKeyValue>();
            if (null != lpcFailsafeLimitKeyValue)
                lpcFailsafeLimit = ScaledNumber.ToLong(lpcFailsafeLimitKeyValue.Value, lpcFailsafeLimitKeyValue.Scale);

            FailsafeProductionActivePowerLimitKeyValue? lppFailsafeLimitKeyValue = local.GetKeyValue<FailsafeProductionActivePowerLimitKeyValue>();
            if (null != lppFailsafeLimitKeyValue)
                lppFailsafeLimit = ScaledNumber.ToLong(lppFailsafeLimitKeyValue.Value, lppFailsafeLimitKeyValue.Scale);

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
                FailSafe = new FailSafeData
                {
                    LimitDuration = (int)failsafeDuration.TotalSeconds
                },
                UseCaseSupport = local.GetUseCaseSupport(),
                ShipId = local.ShipID
            };
        }
        public DeviceData? GetRemoteData(string ski)
        {

            /* The remote devices get garbage collected after some time. After we connected to a device/a device connected to us, it can be that it does not announce itself to us via mdns anymore.
             * After 1 hour, the remote devices get garbage collected and we cannot find the remote in the devices list anymore. Thus, we check for the connection and get the remote from there.
                RemoteDevice? remote = _devices.GetRemotes().FirstOrDefault(r => r.SKI.ToString() == ski);
                if (remote == null) return null;
            */

            if (!_connections.TryGetValue(ski, out Connection? connection))
            {
                return null;
            }
            if (!IsConnectionAlive(connection)) return null;    //remote does not have an active connection anymore

            RemoteDevice? remote = connection.Remote;
            if (remote == null) return null;

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
            mgcpData.PvCurtailmentLimitFactor = pvCurtailmentKeyValue?.Data.value.scaledNumber?.ToLong();

            return new DeviceData
            {
                SKI = remote.SKI.ToString(),
                Measurements = measurements,
                Mgcp = mgcpData,
                UseCaseSupport = remote.GetUseCaseSupport(),
                Name = remote.Name,
            };
        }
        public IEnumerable<RemoteDeviceData> GetDiscoveredDevices()
        {
            return _devices.GetRemotes().Select(rd => new RemoteDeviceData
            {
                Id = rd.Id,
                Name = rd.Name,
                Ski = rd.SKI.ToString(),
            });
        }

        /// <summary>
        /// Writes the provided <paramref name="deviceData"/> to the local device of the connection with device with <paramref name="remoteSki"/>.
        /// </summary>
        /// <param name="deviceData"></param>
        /// <param name="remoteSki"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task WriteDataAsync(DeviceData deviceData, string remoteSki)
        {
            if (!_connections.TryGetValue(remoteSki, out Connection? connection))
            {
                throw new Exception("No active connection for ski " + remoteSki);
            }

            if (deviceData.Lpc != null || deviceData.Lpp != null)
            {
                var loadControlLimitListData = SpineCmdPayloadBase.GetClass("loadControlLimitListData");
                if (loadControlLimitListData != null)
                {
                    await loadControlLimitListData.WriteDataAsync(connection, deviceData);
                }

                var electricalConnectionCharacteristicListData = SpineCmdPayloadBase.GetClass("electricalConnectionCharacteristicListData");
                if (electricalConnectionCharacteristicListData != null)
                {
                    await electricalConnectionCharacteristicListData.WriteDataAsync(connection, deviceData);
                }
            }

            var deviceConfigurationKeyValueListData = SpineCmdPayloadBase.GetClass("deviceConfigurationKeyValueListData");
            if (deviceConfigurationKeyValueListData != null)
            {
                await deviceConfigurationKeyValueListData.WriteDataAsync(connection, deviceData);
            }

            if (deviceData.Measurements != null)
            {
                var measurementListData = SpineCmdPayloadBase.GetClass("measurementListData");
                if (measurementListData != null)
                {
                    await measurementListData.WriteDataAsync(connection, deviceData);
                }
            }
        }
        /// <summary>
        /// Fills the provided <paramref name="deviceData"/> with data read from the remote device identified by <paramref name="remoteSki"/>.
        /// Example: if one wants to read the lpc data, provide a deviceData object where every property is set to null, except the "Lpc" property:
        /// DeviceData dataToRead = new DeviceData {
        ///     Lpc = new LpcLppData() // other properties are null
        /// }
        /// </summary>
        /// <param name="deviceData">The device data object to populate.</param>
        /// <param name="remoteSki">The SKI of the remote device to read data from.</param>
        /// <exception cref="Exception">Thrown if no active connection exists for the specified SKI.</exception>
        public async Task ReadDataAsync(DeviceData deviceData, string remoteSki)
        {
            if (!_connections.TryGetValue(remoteSki, out Connection? connection))
            {
                throw new Exception("No active connection for ski " + remoteSki);
            }
            if (deviceData.Lpc != null || deviceData.Lpp != null)
            {
                var loadControlLimitListData = SpineCmdPayloadBase.GetClass("loadControlLimitListData");
                if (loadControlLimitListData != null)
                {
                    await loadControlLimitListData.ReadDataAsync(connection, deviceData);
                }

                var electricalConnectionCharacteristicListData = SpineCmdPayloadBase.GetClass("electricalConnectionCharacteristicListData");
                if (electricalConnectionCharacteristicListData != null)
                {
                    await electricalConnectionCharacteristicListData.ReadDataAsync(connection, deviceData);
                }
            }
            var deviceConfigurationKeyValueListData = SpineCmdPayloadBase.GetClass("deviceConfigurationKeyValueListData");
            if (deviceConfigurationKeyValueListData != null)
            {
                await deviceConfigurationKeyValueListData.ReadDataAsync(connection, deviceData);
            }
            if (deviceData.Measurements != null)
            {
                var measurementListData = SpineCmdPayloadBase.GetClass("measurementListData");
                if (measurementListData != null)
                {
                    await measurementListData.ReadDataAsync(connection, deviceData);
                }
            }
        }

        /// <summary>
        /// Closes all connections to the device with the given SKI.
        /// </summary>
        /// <param name="ski">SKI of the remote device</param>
        /// <param name="reason">Reason sent in the SHIP close message (SHIP 13.4.8.1.1). Use <see cref="ConnectionCloseReasonType.removedConnection"/> only when the
        /// device is removed from the list of known/accepted devices; the peer will treat a later reconnect like a new device.</param>
        public async Task DisconnectAsync(string ski, ConnectionCloseReasonType reason = ConnectionCloseReasonType.unspecific)
        {
            bool found = false;
            if (_duplicateConnections.TryRemove(ski, out Connection? duplicate) && duplicate != null)
            {
                found = true;
                await CloseConnectionAsync(duplicate, reason).ConfigureAwait(false);
            }

            if (_connections.TryRemove(ski, out Connection? connection) && connection != null)
            {
                found = true;
                await CloseConnectionAsync(connection, reason).ConfigureAwait(false);
            }

            if (!found)
            {
                _logger?.LogInformation("No connection found for ski {Ski}.", ski);
            }
        }

        public void Start()
        {
            var localDevice = _devices.Local;
            _mDNSService.Run(localDevice, _cts.Token);
            _mDNSClient.Run(_devices);

            _shipListener = new SHIPListener(_devices, _settings, _logger);
            _shipListener.OnNewConnectionValidation = (args) =>
            {
                RemoteDevice? device = _devices.GetRemotes().FirstOrDefault(rd => rd.SKI.ToString() == args.Ski);
                if (null == device)
                {
                    _logger?.LogError($"remote device with SKI {args.Ski} has no mDNS advertisements");
                    return false;
                }

                var paired = _devices.GetPairedDevices().FirstOrDefault(p => p.TrustId == device.Id);

                bool certificateValid = paired != null //device was added via ship pairing
                    ? CertificateMatchesFingerprint(device, args.Certificate, paired.TrustPar)
                    : CertificateMatchesSki(device, args.Certificate);

                return certificateValid && IsTrusted(args.Ski);
            };
            _shipListener.OnConnectionIncoming = OnConnectionIncomingAsync;
            _shipListener.StartAsync(_settings.Device.Port);
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
            string[] skis = _connections.Keys.Union(_duplicateConnections.Keys).ToArray();
            foreach (var ski in skis)
            {
                try
                {
                    await DisconnectAsync(ski).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("EEBUSManager.StopAsync: disconnect failed for " + ski + ": " + ex.Message);
                }
            }
        }
        #endregion

        #region Testing methods
        internal Connection? GetConnection(string ski)
        {
            _connections.TryGetValue(ski, out Connection? connection);
            return connection;
        }
        internal bool HasDuplicateConnection(string ski)
        {
            return _duplicateConnections.ContainsKey(ski);
        }
        private void OnServerStateChanged(Connection.EState state, RemoteDevice device)
        {
            this.ServerStateChanged?.Invoke(this, new StateChangedEventArgs(state, device));
        }
        private void OnClientStateChanged(Connection.EState state, RemoteDevice device)
        {
            this.ClientStateChanged?.Invoke(this, new StateChangedEventArgs(state, device));
        }
        #endregion
    }
}
