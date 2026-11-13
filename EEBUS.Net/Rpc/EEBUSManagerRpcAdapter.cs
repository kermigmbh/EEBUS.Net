using Makaretu.Dns;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.IO.Abstractions;
using System.Net;
using System.Net.Sockets;

namespace EEBUS.Net.Rpc;

/// <summary>
/// Owns the <see cref="EEBUSManager"/> steered by the RPC methods, so that methods like <c>Reboot</c> can replace it.
/// The constructor parameters are used for every manager instance; <c>configure</c> is applied to each new instance before it is started
/// (e.g. to register event handlers).
/// </summary>
public sealed class EEBUSManagerRpcAdapter : IDisposable
{
    private readonly Settings _settings;
    private readonly Action<EEBUSManager>? _configure;
    private readonly ServiceDiscovery? _serviceDiscovery;
    private readonly ILogger? _logger;
    private readonly IFileSystem? _fileSystem;
    private readonly TimeProvider? _timeProvider;

    public EEBUSManagerRpcAdapter(Settings settings, Action<EEBUSManager>? configure = null, ServiceDiscovery? serviceDiscovery = null,
        ILogger? logger = null, IFileSystem? fileSystem = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        _configure = configure;
        _serviceDiscovery = serviceDiscovery;
        _logger = logger;
        _fileSystem = fileSystem;
        _timeProvider = timeProvider;

        Manager = CreateManager();
    }

    private Task _reboot = Task.CompletedTask;

    /// <summary>How often <see cref="WaitForOnlineAsync"/> probes the SHIP port.</summary>
    public static readonly TimeSpan OnlinePollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>The current, started manager. Replaced by <see cref="Reboot"/> and <see cref="FactoryReset"/>, so don't hold on to it.</summary>
    public EEBUSManager Manager { get; private set; }

    /// <summary>
    /// The last reboot or factory reset. Completes once the new manager is started, faults if the restart failed.
    /// Already completed if neither was requested.
    /// </summary>
    public Task RebootTask => _reboot;

    /// <summary>
    /// Accepts a reboot and returns immediately; the current manager is disposed and a new one with the same settings and trusted SKIs
    /// is started in the background (see <see cref="RebootTask"/>). Does nothing if a reboot is already running.
    /// </summary>
    /// <remarks>
    /// Not thread-safe: the check for a running reboot has no lock, so two calls at exactly the same time could both start a reboot.
    /// V360 sends its calls one after another, so this doesn't happen there.
    /// <para>
    /// While a reboot is running, <see cref="Manager"/> still returns the old, possibly already disposed manager.
    /// V360 always calls <c>WaitForOnline</c> right after <c>Reboot</c>, so no other calls reach it in the meantime.
    /// </para>
    /// </remarks>
    // TODO: A reboot must keep all persisted state; currently only the trusted SKIs (and the certificate on disk) survive it.
    // - SHIP pairing data (Devices._paired incl. the certificate fingerprint trustPar) is lost. The spec lists it as persisted state
    //   that only a factory reset erases, so it has to be carried over like the trusted SKIs (needs an internal accessor on EEBUSManager).
    // - Failsafe values written at runtime (failsafe limit, failsafe duration minimum) fall back to Settings.InitLimits.
    //   Until they are carried over, "Persists Failsafe Values" must be declared false in the V360 configuration.
    public void Reboot() => Restart(keepTrustedSkis: true);

    /// <summary>
    /// Accepts a factory reset and returns immediately; like <see cref="Reboot"/>, but the new manager starts without the trusted SKIs.
    /// Pairing data and failsafe values only live in the manager, so the new instance starts with none and with the configured defaults.
    /// The certificate on disk is kept, so the device's SKI doesn't change.
    /// </summary>
    /// <remarks>
    /// Same caveats as <see cref="Reboot"/>. In addition, a factory reset requested while a reboot is still running is ignored.
    /// V360 always calls <c>WaitForOnline</c> in between, so this doesn't happen there.
    /// </remarks>
    public void FactoryReset() => Restart(keepTrustedSkis: false);

    private void Restart(bool keepTrustedSkis)
    {
        if (!_reboot.IsCompleted)
            return;

        _reboot = Task.Run(() =>
        {
            try
            {
                // The trust store only lives in memory, but a reboot must keep it (only a factory reset clears it).
                List<string>? trustedSkis = keepTrustedSkis ? Manager.GetTrustedSkis() : null;

                // Dispose blocks until all connections are closed.
                Manager.Dispose();
                Manager = CreateManager(trustedSkis);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, keepTrustedSkis ? "Reboot failed." : "Factory reset failed.");
                throw;
            }
        });
    }

    /// <summary>
    /// Waits until a running reboot or factory reset has finished and the SHIP port accepts TCP connections, polling every
    /// <see cref="OnlinePollInterval"/>. If the last restart failed, its exception is rethrown instead.
    /// </summary>
    /// <exception cref="TimeoutException">The device is not online within <paramref name="timeout"/>.</exception>
    public async Task WaitForOnlineAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        TimeProvider timeProvider = _timeProvider ?? TimeProvider.System;
        using CancellationTokenSource timeoutCts = new(timeout, timeProvider);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            await _reboot.WaitAsync(linkedCts.Token).ConfigureAwait(false);

            while (!await IsShipPortOpenAsync(linkedCts.Token).ConfigureAwait(false))
                await Task.Delay(OnlinePollInterval, timeProvider, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException(string.Create(CultureInfo.InvariantCulture, $"Device not online within {timeout.TotalSeconds} seconds."));
        }
    }

    /// <summary>Waits for a running reboot, then disposes the current manager.</summary>
    public void Dispose()
    {
        try
        {
            _reboot.GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // The failed reboot already disposed the old manager and didn't leave a new one running.
            return;
        }

        Manager.Dispose();
    }

    private async Task<bool> IsShipPortOpenAsync(CancellationToken ct)
    {
        using TcpClient client = new();
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, _settings.Device.Port, ct).ConfigureAwait(false);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private EEBUSManager CreateManager(IEnumerable<string>? trustedSkis = null)
    {
        EEBUSManager manager = new(_settings, _serviceDiscovery, _logger, _fileSystem, _timeProvider);
        try
        {
            foreach (string ski in trustedSkis ?? [])
                manager.AddTrustedSki(ski);

            _configure?.Invoke(manager);
            manager.Start();
        }
        catch
        {
            manager.Dispose();
            throw;
        }

        return manager;
    }
}
