using EEBUS.Enums;
using EEBUS.Models;
using EEBUS.SHIP.Messages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;

namespace EEBUS.Net.Connections;

/// <summary>
/// Owns the lifecycle of all SHIP connections (one per remote SKI): opening client connections, registering incoming ones,
/// resolving double connections (SHIP 12.2.3), closing, periodic reconnects of trusted devices and publishing status changes.
/// </summary>
internal sealed class ConnectionManager(Devices devices, TrustStore trust, X509Certificate2 certificate, TimeProvider timeProvider, ILogger? logger)
{
    private const int DoubleConnectionTimeoutMs = 3000;   //SHIP 12.2.3: time the node with the smaller SKI waits for the bigger one to resolve a double connection
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StatusInterval = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<string, Connection> _connections = new();
    //SHIP 12.2.3: older connection to a SKI that is held while a double connection is being resolved by the peer (we have the smaller SKI)
    private readonly ConcurrentDictionary<string, Connection> _duplicateConnections = new();
    //Contains only SKIs whose last published status was not Unknown
    private readonly ConcurrentDictionary<string, (DeviceConnectionStatus Status, RemoteDevice? Remote)> _lastPublishedStatus = new();

    /// <summary>
    /// Raised from the status loop whenever the connection status of a remote device changes.
    /// </summary>
    public Func<RemoteDevice, DeviceConnectionStatus, Task>? StatusChanged { get; set; }

    public IReadOnlyList<Connection> ActiveConnections => [.. _connections.Values];

    public Connection? Get(string ski)
    {
        _connections.TryGetValue(ski, out Connection? connection);
        return connection;
    }

    public bool IsConnected(string ski) => Get(ski)?.IsAlive == true;

    public bool HasDuplicate(string ski) => _duplicateConnections.ContainsKey(ski);

    public DeviceConnectionStatus GetStatus(string ski)
        => Get(ski) is { IsAlive: true } connection ? connection.ConnectionStatus : DeviceConnectionStatus.Unknown;

    /// <summary>
    /// Starts the background loops (status publishing and, optionally, periodic reconnects). Both end when <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    public void StartBackgroundTasks(bool periodicReconnect, CancellationToken cancellationToken)
    {
        if (periodicReconnect)
            _ = RunPeriodicallyAsync(ReconnectInterval, ReconnectTrustedDevicesAsync, "reconnect", cancellationToken);

        _ = RunPeriodicallyAsync(StatusInterval, _ => PublishStatusChangesAsync(), "status", cancellationToken);
    }

    #region Connect / disconnect
    /// <summary>
    /// Opens a client connection to the remote device with the given <paramref name="ski"/> unless a live connection exists already.
    /// Returns true once the connection has been registered and its receive loop has ended.
    /// </summary>
    public async Task<bool> ConnectAsync(string ski, CancellationToken cancellationToken)
    {
        SKI remoteSki = new(ski);
        RemoteDevice? device = devices.GetRemotes().FirstOrDefault(rd => rd.SKI == remoteSki);
        if (device == null || IsConnected(ski))
            return false;

        Uri uri = new("wss://" + device.Url);
        ClientWebSocket? wsClient = null;
        try
        {
            wsClient = new ClientWebSocket();
            wsClient.Options.AddSubProtocol("ship");
            wsClient.Options.KeepAliveInterval = TimeSpan.FromMilliseconds(SHIPMessageTimeout.WS_KEEP_ALIVE_INTERVAL);
            wsClient.Options.KeepAliveTimeout = TimeSpan.FromMilliseconds(SHIPMessageTimeout.WS_KEEP_ALIVE_TIMEOUT);
            wsClient.Options.RemoteCertificateValidationCallback = (_, cert, _, _) => trust.ValidateServerCertificate(device, cert);
            wsClient.Options.ClientCertificates.Add(certificate);

            //ConnectAsync either completes with the socket in the Open state or throws; failures are handled in the catch blocks below.
            await wsClient.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);

            Client client = new(new HostString(uri.Host, uri.Port), wsClient, devices, device, timeProvider, logger);
            wsClient = null;    //Ownership of the websocket is transferred to the Client/Connection
            if (!await RegisterAsync(client).ConfigureAwait(false))
                return false;

            logger?.LogDebug("Added client with ski {Ski} to connections.", ski);
            await client.Run(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Connect error for SKI {Ski}.", ski);
            return false;
        }
        finally
        {
            wsClient?.Dispose();
        }
    }

    /// <summary>
    /// Registers an incoming connection accepted by the SHIP listener.
    /// </summary>
    public async Task AcceptAsync(Server server)
    {
        if (server.Remote != null && await RegisterAsync(server).ConfigureAwait(false))
            logger?.LogDebug("Added connection with ski {Ski} to connections.", server.Remote.SKI);
    }

    /// <summary>
    /// Closes all connections to the device with the given SKI. Returns false if no connection was found.
    /// </summary>
    public async Task<bool> DisconnectAsync(string ski, ConnectionCloseReasonType reason = ConnectionCloseReasonType.unspecific)
    {
        bool found = false;
        if (_duplicateConnections.TryRemove(ski, out Connection? duplicate))
        {
            found = true;
            await CloseAsync(duplicate, reason).ConfigureAwait(false);
        }

        if (_connections.TryRemove(ski, out Connection? connection))
        {
            found = true;
            await CloseAsync(connection, reason).ConfigureAwait(false);
        }

        return found;
    }

    /// <summary>
    /// Best-effort disconnect of every active and pending duplicate connection.
    /// </summary>
    public async Task DisconnectAllAsync()
    {
        foreach (string ski in _connections.Keys.Union(_duplicateConnections.Keys).ToArray())
        {
            try
            {
                await DisconnectAsync(ski).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Disconnect failed for ski {Ski}.", ski);
            }
        }
    }
    #endregion

    #region Bookkeeping and double connections (SHIP 12.2.3)
    /// <summary>
    /// Registers <paramref name="newConnection"/> as the connection for its remote SKI and resolves double connections according to SHIP 12.2.3:
    /// the node with the bigger SKI keeps only the most recent connection and closes all others. The node with the smaller SKI keeps both
    /// connections for up to <see cref="DoubleConnectionTimeoutMs"/> and lets the peer decide; only if the peer does not act, the older connection is closed.
    /// Returns false if <paramref name="newConnection"/> was closed instead of being registered.
    /// </summary>
    private async Task<bool> RegisterAsync(Connection newConnection)
    {
        if (newConnection.Remote == null) return false;

        //The connection reports its own end (client and server alike), which removes it from the bookkeeping again
        newConnection.Closed = OnConnectionClosedAsync;

        string remoteSki = newConnection.Remote.SKI.ToString();
        while (!_connections.TryAdd(remoteSki, newConnection))
        {
            if (Get(remoteSki) is not Connection existing)
                continue;

            if (!existing.IsAlive || devices.Local.SKI > newConnection.Remote.SKI)
            {
                //Stale connection, or we have the bigger SKI: keep the most recent connection and close the old one (SHIP 12.2.3)
                if (!_connections.TryRemove(new KeyValuePair<string, Connection>(remoteSki, existing)))
                    continue;   //connection was already removed in the meantime, try to add the new connection again

                string reason = existing.IsAlive ? "local SKI is bigger" : "existing connection is stale";
                logger?.LogDebug("Double connection with remote device {Name} detected, closing connection with ski {Ski} ({Reason})", newConnection.Remote.Name, remoteSki, reason);
                await CloseAsync(existing).ConfigureAwait(false);
                continue;
            }

            //We have the smaller SKI: the peer decides which connection survives. Keep the older connection in the side table
            //and wait for the peer to close it; if it does not do so within the timeout, we close it ourselves.
            if (!_connections.TryUpdate(remoteSki, newConnection, existing))
                continue;

            if (_duplicateConnections.TryRemove(remoteSki, out Connection? previousDuplicate))
            {
                //more than two connections to the same node - close the oldest one, the peer should have dealt with it already
                await CloseAsync(previousDuplicate).ConfigureAwait(false);
            }
            _duplicateConnections[remoteSki] = existing;

            logger?.LogDebug("Double connection with remote device {Name} detected, waiting for peer to close one of them (remote SKI is bigger)", newConnection.Remote.Name);
            _ = ResolveDoubleConnectionAfterTimeoutAsync(remoteSki);
            break;
        }

        return true;
    }

    /// <summary>
    /// SHIP 12.2.3: after the timeout, the node with the smaller SKI checks both connections, closes the ones that are not alive anymore
    /// and MAY close the older one if both are still alive.
    /// </summary>
    private async Task ResolveDoubleConnectionAfterTimeoutAsync(string ski)
    {
        //TODO: SHIP 12.2.3 requires a WebSocket ping after the timeout; the managed WebSocket only supports keep-alive pings
        await Task.Delay(DoubleConnectionTimeoutMs).ConfigureAwait(false);

        try
        {
            if (!_duplicateConnections.TryRemove(ski, out Connection? older))
                return; //already resolved

            if (older.IsAlive && !IsConnected(ski))
            {
                //The peer kept the older connection instead (or the newer one died); make the older one the active one again
                await PromoteAsync(ski, older).ConfigureAwait(false);
                return;
            }

            //Either the peer closed the older connection as expected, or both are alive and the peer did not act -> close the older one
            if (older.IsAlive)
                logger?.LogDebug("Peer did not resolve double connection to ski {ski} within {timeout} ms, closing older connection", ski, DoubleConnectionTimeoutMs);
            await CloseAsync(older).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error resolving double connection for ski {Ski}.", ski);
        }
    }

    private async Task PromoteAsync(string ski, Connection older)
    {
        if (!older.IsAlive)
        {
            await CloseAsync(older).ConfigureAwait(false);
            return;
        }

        if (Get(ski) is { IsAlive: false } current && _connections.TryRemove(new KeyValuePair<string, Connection>(ski, current)))
            await CloseAsync(current).ConfigureAwait(false);

        if (!_connections.TryAdd(ski, older))
        {
            await CloseAsync(older).ConfigureAwait(false);
            return;
        }

        logger?.LogDebug("Peer kept the older connection to ski {ski}, promoting it", ski);
    }

    /// <summary>
    /// Called by every registered connection once its receive loop has ended. Removes it from the bookkeeping
    /// and promotes a pending duplicate, if the peer kept the older connection.
    /// </summary>
    private async Task OnConnectionClosedAsync(Connection connection)
    {
        if (connection.Remote == null) return;

        string ski = connection.Remote.SKI.ToString();
        //Only remove the entry if it still refers to this connection; it may have been replaced in the meantime
        if (_connections.TryRemove(new KeyValuePair<string, Connection>(ski, connection)))
        {
            if (_duplicateConnections.TryRemove(ski, out Connection? older))
                await PromoteAsync(ski, older).ConfigureAwait(false);
        }
        else
        {
            _duplicateConnections.TryRemove(new KeyValuePair<string, Connection>(ski, connection));
        }
    }

    /// <summary>
    /// Sends a SHIP close message (if connected), closes and disposes the websocket. The connection must already have been removed from the bookkeeping.
    /// </summary>
    private async Task CloseAsync(Connection connection, ConnectionCloseReasonType reason = ConnectionCloseReasonType.unspecific)
    {
        string ski = connection.Remote?.SKI.ToString() ?? string.Empty;
        WebSocket? ws = connection.WebSocket;
        logger?.LogDebug("Closing connection with ski {Ski}", ski);
        try
        {
            if (ws?.State == WebSocketState.Open)
            {
                if (connection.State == Connection.EState.Connected)
                {
                    CloseMessage closeMessage = new(ConnectionClosePhaseType.announce);
                    closeMessage.connectionClose.reason = reason;
                    await connection.PushCloseMessageAsync(closeMessage);
                }

                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Disconnect error for connection with ski {Ski}.", ski);
        }
        finally
        {
            //We need to dispose the websocket in any case, e.g. we sent a close message but the peer never answered
            await connection.CloseAsync();
            try { ws?.Dispose(); } catch { }
        }
    }
    #endregion

    #region Background loops
    private async Task RunPeriodicallyAsync(TimeSpan interval, Func<CancellationToken, Task> action, string name, CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await action(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error in {Loop} loop", name);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task ReconnectTrustedDevicesAsync(CancellationToken cancellationToken)
    {
        logger?.LogDebug("[EEBUS] Running Reconnect Loop. Current connections: {Count}", _connections.Count);
        foreach (RemoteDevice device in devices.GetRemotes())
        {
            string ski = device.SKI.ToString();
            if (cancellationToken.IsCancellationRequested || !trust.IsTrusted(ski) || IsConnected(ski))
                continue;

            logger?.LogDebug("Device {deviceName} does not have an active connection anymore, reconnecting...", device.Name);
            await ConnectAsync(ski, cancellationToken).ConfigureAwait(false);   //handles and logs all errors itself
        }
    }

    /// <summary>
    /// Compares the status of every known SKI with the last published one and reports changes. Runs on a single loop,
    /// so events are serialized and always reflect the current state.
    /// </summary>
    private async Task PublishStatusChangesAsync()
    {
        foreach (string ski in _connections.Keys.Union(_lastPublishedStatus.Keys).ToArray())
        {
            DeviceConnectionStatus status = GetStatus(ski);
            bool wasPublished = _lastPublishedStatus.TryGetValue(ski, out var last);
            if (status == (wasPublished ? last.Status : DeviceConnectionStatus.Unknown))
                continue;

            RemoteDevice? remote;
            if (status == DeviceConnectionStatus.Unknown)
            {
                //Connection is gone, use the remote stored when it was last online
                remote = last.Remote;
                _lastPublishedStatus.TryRemove(ski, out _);
            }
            else
            {
                remote = Get(ski)?.Remote ?? last.Remote;
                _lastPublishedStatus[ski] = (status, remote);
            }

            if (remote == null || StatusChanged == null)
                continue;

            try
            {
                await StatusChanged(remote, status).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error in OnDeviceConnectionStatusChanged handler for device {ski} ({status})", ski, status);
            }
        }
    }
    #endregion
}
