using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net.Connections;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.Net.Events;
using EEBUS.Net.Extensions;
using EEBUS.Net.Handlers;
using EEBUS.SHIP.Messages;
using EEBUS.StateMachines;
using EEBUS.UseCases;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace EEBUS.Net;

/// <summary>
/// Entry point of the library: advertises the local device, discovers remote devices and manages SHIP connections to them.
/// </summary>
public class EEBUSManager : IDisposable
{
    private static readonly string[] RegisteredNamespaces =
    [
        "EEBUS.SHIP.Messages", "EEBUS.SPINE.Commands", "EEBUS.Entities",
        "EEBUS.UseCases.ControllableSystem", "EEBUS.UseCases.EnergyGuard", "EEBUS.UseCases.GridConnectionPoint",
        "EEBUS.UseCases.MonitoringAppliance", "EEBUS.UseCases.MonitoredUnit", "EEBUS.Features"
    ];

    private readonly Settings _settings;
    private readonly IFileSystem _fileSystem;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly X509Certificate2 _cert;

    private readonly Devices _devices;
    private readonly ServiceDiscovery _serviceDiscovery;
    private readonly bool _ownsServiceDiscovery;
    private readonly MDNSClient _mDNSClient;
    private readonly MDNSService _mDNSService;
    private SHIPListener? _shipListener;

    private readonly TrustPolicy _trust;
    private readonly ConnectionManager _connections;

    private readonly LpcLimitStateMachine _lpcStateMachine;
    private readonly LppLimitStateMachine _lppStateMachine;
    private readonly UseCaseEvents[] _localDeviceEvents;

    #region Public events
    public event EventHandler<RemoteDevice>? OnDeviceFound;
    public Func<LimitDataChangedEventArgs, Task>? OnLimitDataChanged;
    public Func<DeviceData, Task>? OnDeviceDataChanged { get; set; }
    public Func<RemoteDevice, DeviceConnectionStatus, Task>? OnDeviceConnectionStatusChanged { get; set; }
    #endregion

    #region Testing events
    internal Func<ShipMessageBase, Connection, Task<bool>>? BeforeMessageSent { get; set; }
    internal event EventHandler<StateChangedEventArgs>? ClientStateChanged;
    internal event EventHandler<StateChangedEventArgs>? ServerStateChanged;
    #endregion

    public Devices Devices => _devices;

    static EEBUSManager()
    {
        //Run the static constructors of all classes in the EEBUS namespaces so the static registration of data structures, features, key values and use cases is executed.
        foreach (Type type in typeof(Settings).Assembly.GetTypes().Where(t => RegisteredNamespaces.Contains(t.Namespace)))
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
    }

    public EEBUSManager(Settings settings, ServiceDiscovery? serviceDiscovery = null, ILogger? logger = null, IFileSystem? fileSystem = null, TimeProvider? timeProvider = null)
    {
        _settings = settings;
        _fileSystem = fileSystem ?? new FileSystem();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;

        _ownsServiceDiscovery = serviceDiscovery == null;
        _serviceDiscovery = serviceDiscovery ?? EEBusServiceDiscovery.Create(settings.FilterLinkLocalAddresses);

        _cert = CertificateGenerator.GenerateCert(_fileSystem, settings.BasePath, settings.Certificate);
        byte[] ski = SHA1.HashData(_cert.GetPublicKey());
        string fingerprint = Convert.ToHexString(SHA256.HashData(_cert.GetRawCertData()));  //Fingerprint is used for ship pairing

        _devices = new Devices();
        LocalDevice localDevice = _devices.GetOrCreateLocal(ski, settings.Device, fingerprint);
        _devices.RemoteDeviceFound += OnRemoteDeviceFound;
        _devices.ServerStateChanged += OnServerStateChanged;
        _devices.ClientStateChanged += OnClientStateChanged;

        _trust = new TrustPolicy(_devices, settings, logger);
        _connections = new ConnectionManager(_devices, _trust, _cert, _timeProvider, logger)
        {
            StatusChanged = (remote, status) => OnDeviceConnectionStatusChanged?.Invoke(remote, status) ?? Task.CompletedTask
        };

        //Every ship node advertises its own ship service instance and listens to incoming ship service instances
        _mDNSClient = new MDNSClient(_serviceDiscovery, () => _trust.CanEvaluateShipPairingRequests(_connections.IsConnected), logger);
        _mDNSService = new MDNSService(settings.Device.Id, settings.Device.Port, _serviceDiscovery, settings.FilterLinkLocalAddresses);

        //Register event handlers. The events can be triggered by calling localDevice.GetUseCaseEvents()
        _lpcStateMachine = new LpcLimitStateMachine(localDevice, logger);
        _lpcStateMachine.RegisterEventHandler(new LpcEventHandler(this, logger));
        _lppStateMachine = new LppLimitStateMachine(localDevice);
        _lppStateMachine.RegisterEventHandler(new LppEventHandler(this, logger));
        _localDeviceEvents =
        [
            _lpcStateMachine,
            _lppStateMachine,
            new NotifyEventHandler(_connections),
            new MonitoringEventHandler(this),
            new DeviceConnectionStatusEventHandler(logger),
            new MessageEventHandler(this)
        ];

        foreach (UseCaseEvents events in _localDeviceEvents)
            localDevice.AddUseCaseEvents(events);

        _connections.StartBackgroundTasks(settings.EnablePeriodicReconnect, _cts.Token);
    }

    #region Lifecycle
    public void Start()
    {
        _mDNSService.Run(_devices.Local, _cts.Token);
        _mDNSClient.Run(_devices);

        _shipListener = new SHIPListener(_devices, _settings, _fileSystem, _timeProvider, _logger)
        {
            OnNewConnectionValidation = _trust.ValidateIncomingConnection,
            OnConnectionIncoming = _connections.AcceptAsync
        };
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
                _logger?.LogWarning(ex, "SHIPListener stop failed.");
            }
        }

        _mDNSClient.Stop();
        await _connections.DisconnectAllAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        // Cancel first so background loops (reconnect, status, mDNS) start unwinding before we wait on graceful shutdown.
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }

        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "StopAsync failed during dispose.");
        }

        _devices.RemoteDeviceFound -= OnRemoteDeviceFound;
        _devices.ServerStateChanged -= OnServerStateChanged;
        _devices.ClientStateChanged -= OnClientStateChanged;

        foreach (UseCaseEvents events in _localDeviceEvents)
            _devices.Local.RemoveUseCaseEvents(events);

        _lpcStateMachine.Dispose();
        _lppStateMachine.Dispose();

        if (_ownsServiceDiscovery)
            _serviceDiscovery.Dispose();

        try { _cert.Dispose(); } catch { }
        try { _cts.Dispose(); } catch { }
    }
    #endregion

    #region Trust
    public void AddTrustedSki(string ski) => _trust.Trust(ski);

    public void RemoveTrustedSki(string ski) => _trust.Distrust(ski);
    #endregion

    #region Connections
    /// <summary>
    /// Opens a client connection to the remote device with the given <paramref name="ski"/> unless a live connection exists already.
    /// Returns true once the connection has been registered and its receive loop has ended.
    /// </summary>
    public Task<bool> TryConnectAsync(string ski) => _connections.ConnectAsync(ski, _cts.Token);

    /// <summary>
    /// Closes all connections to the device with the given SKI.
    /// </summary>
    /// <param name="ski">SKI of the remote device</param>
    /// <param name="reason">Reason sent in the SHIP close message (SHIP 13.4.8.1.1). Use <see cref="ConnectionCloseReasonType.removedConnection"/> only when the
    /// device is removed from the list of known/accepted devices; the peer will treat a later reconnect like a new device.</param>
    public async Task DisconnectAsync(string ski, ConnectionCloseReasonType reason = ConnectionCloseReasonType.unspecific)
    {
        if (!await _connections.DisconnectAsync(ski, reason).ConfigureAwait(false))
            _logger?.LogInformation("No connection found for ski {Ski}.", ski);
    }

    public DeviceConnectionStatus GetConnectionStatus(string ski) => _connections.GetStatus(ski);

    public string GetConnectionInfo(string ski)
    {
        Connection? connection = _connections.Get(ski);
        if (connection == null) return "No active connection for ski " + ski;

        StringBuilder sb = new();
        sb.AppendLine("Connection type: " + (connection is Client ? "Client" : "Server") + ";");
        sb.AppendLine("Websocket state: " + connection.WebSocket.State + ";");
        sb.AppendLine("Is trusted: " + _trust.IsTrusted(ski) + ";");
        sb.AppendLine("Connection state: " + connection.State + " - " + connection.SubState + ";");
        return sb.ToString();
    }
    #endregion

    #region Device data
    public DeviceData GetLocalData()
        => _devices.Local?.ToDeviceData(_lpcStateMachine.GetEffectiveLimit(), _lppStateMachine.GetEffectiveLimit()) ?? new();

    public DeviceData? GetRemoteData(string ski)
    {
        /* The remote devices get garbage collected after some time. After we connected to a device/a device connected to us, it can be that it does not announce itself to us via mdns anymore.
         * After 1 hour, the remote devices get garbage collected and we cannot find the remote in the devices list anymore. Thus, we check for the connection and get the remote from there. */
        return _connections.Get(ski) is { IsAlive: true, Remote: { } remote } ? remote.ToDeviceData() : null;
    }

    public IEnumerable<RemoteDeviceData> GetDiscoveredDevices()
        => _devices.GetRemotes().Select(rd => new RemoteDeviceData { Id = rd.Id, Name = rd.Name, Ski = rd.SKI.ToString() });

    /// <summary>
    /// Writes the provided <paramref name="deviceData"/> to the local device of the connection with device with <paramref name="remoteSki"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if no active connection exists for the specified SKI.</exception>
    public Task WriteDataAsync(DeviceData deviceData, string remoteSki) => GetRequiredConnection(remoteSki).WriteAsync(deviceData);

    /// <summary>
    /// Fills the provided <paramref name="deviceData"/> with data read from the remote device identified by <paramref name="remoteSki"/>.
    /// Example: if one wants to read the lpc data, provide a deviceData object where every property is set to null, except the "Lpc" property:
    /// DeviceData dataToRead = new DeviceData {
    ///     Lpc = new LpcLppData() // other properties are null
    /// }
    /// </summary>
    /// <param name="deviceData">The device data object to populate.</param>
    /// <param name="remoteSki">The SKI of the remote device to read data from.</param>
    /// <exception cref="InvalidOperationException">Thrown if no active connection exists for the specified SKI.</exception>
    public Task ReadDataAsync(DeviceData deviceData, string remoteSki) => GetRequiredConnection(remoteSki).ReadAsync(deviceData);

    private Connection GetRequiredConnection(string ski)
        => _connections.Get(ski) ?? throw new InvalidOperationException("No active connection for ski " + ski);
    #endregion

    #region Testing methods
    internal Connection? GetConnection(string ski) => _connections.Get(ski);

    internal bool HasDuplicateConnection(string ski) => _connections.HasDuplicate(ski);
    #endregion

    #region Event plumbing
    internal Task RaiseDeviceDataChangedAsync(DeviceData deviceData) => OnDeviceDataChanged?.Invoke(deviceData) ?? Task.CompletedTask;

    private void OnRemoteDeviceFound(RemoteDevice device)
    {
        _logger?.LogInformation("Remote device found: {deviceName} ({deviceSki})", device.Name, device.SKI.ToString());
        OnDeviceFound?.Invoke(this, device);
    }

    private void OnServerStateChanged(Connection.EState state, RemoteDevice device)
        => ServerStateChanged?.Invoke(this, new StateChangedEventArgs(state, device));

    private void OnClientStateChanged(Connection.EState state, RemoteDevice device)
        => ClientStateChanged?.Invoke(this, new StateChangedEventArgs(state, device));
    #endregion
}
