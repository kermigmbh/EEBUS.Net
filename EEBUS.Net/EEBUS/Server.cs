using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;

using Microsoft.AspNetCore.Http;

using EEBUS.Messages;
using EEBUS.Models;

namespace EEBUS
{
    /// <summary>
    /// Eine Server Instanz verwaltet eine Websocket Instanz, die mit einem wss:// Aufruf von einem anderen Device entstanden ist
    /// Es wird auf ankommende Nachrichten gewartet und meist mit Echo darauf reagiert
    /// </summary>
    public class Server : Connection
    {

        public Server(string ski, HostString host, WebSocket ws, Devices devices)
            : base(host, ws, devices)
        {
            this._ski = ski ?? string.Empty;

            Server? previous = null;
            lock (mutex)
            {
                // If there is already a Server registered for this SKI, capture it so
                // we can close it instead of silently dropping the reference (which
                // would leak the WebSocket and any background tasks rooted by it).
                serverMap.TryGetValue(this._ski, out previous);
                serverMap[this._ski] = this;
            }

            if (previous != null && !ReferenceEquals(previous, this))
            {
                // Fire and forget: we don't want the new connection setup to block
                // on tearing down the stale one, but we must not leak it.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await previous.CloseAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Failed to close previous Server for SKI " + this._ski + ": " + ex.Message);
                    }
                });
            }
        }

        private readonly string _ski;

        static private ConcurrentDictionary<string, Server> serverMap = new();

        static private object mutex = new();

        static public Server? Get(string ski)
        {
            return serverMap.TryGetValue(ski, out Server? server) ? server : null;
        }

        public async Task Do(CancellationToken cancellationToken)
        {
			Debug.WriteLine("Starting Server for device " + this.Remote?.Name);
            var heart = new HeartBeatTask();
            Timer beat = new System.Threading.Timer(arg => heart.Beat(arg), this, 4000, 4000);

            //var ecc        = new ElectricalConnectionCharacteristicTask();
            //using var eccSend   = new System.Threading.Timer( ecc.SendData, this, 2000, Timeout.Infinite );

            //var md         = new MeasurementDataTask();
            //using var mdSend   = new System.Threading.Timer( md.SendData, this, 3000, 3000 );


            try
            {
                while (this.ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var message = await ReceiveAsync(cancellationToken);

                    (this.state, this.subState, string error) = message.ServerTest(this.state);

                    if (this.state == EState.Stopped && error != null)
                        throw new Exception(error);
                    if (error != null)
                        Console.WriteLine(error);

                    EState oldState = this.state;
                    (this.state, this.subState) = await message.NextServerState(this).ConfigureAwait(false);

                    if (null == this.Remote)
                    {
                        var id = message.GetId();
                        if (id != null)
                        {
                            this.Remote = GetRemote(message.GetId());
                        }
                    }

                    if (null != this.Remote)
                        this.Remote.SetServerState(this.state);

                    if (this.state == EState.Connected && this.state != oldState)
                    {
                        RequestRemoteDeviceConfiguration();
                        // ReadAndSubscribe();
                    }

                    ResolvePendingRequest(message);

                    if (this.state == EState.Stopped)
                        throw new Exception("Communication stopped!");
                }
            }
            catch (Exception ex)
            {
                if (null != this.Remote)
                {
                    this.Remote.SetServerState(EState.Stopped);
                    this.Remote.LastDisconnectUtc = DateTime.UtcNow;
                }

                Debug.WriteLine("Exception: " + ex.Message);
            }
            finally
            {
                // Dispose the timer (Change(Infinite, Infinite) only stops further
                // callbacks; the underlying TimerQueueTimer is only released by Dispose).
                try { beat.Dispose(); } catch { }
            }
            //eccSend.Change( Timeout.Infinite, Timeout.Infinite );

            await CloseAsync().ConfigureAwait(false);
        }

        public override async Task CloseAsync()
        {
            try
            {
                await this.ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing!", CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }

            // Only remove from the map if we are still the registered Server for
            // this SKI. A newer connection for the same peer may have replaced us
            // already, and in that case we must not remove its entry.
            lock (mutex)
            {
                if (serverMap.TryGetValue(this._ski, out Server? current) && ReferenceEquals(current, this))
                {
                    serverMap.TryRemove(this._ski, out _);
                }
            }

            this.state = EState.Disconnected;
            this.subState = ESubState.None;

            Debug.WriteLine($"Closed websocket for connectedNode {this.host}. Remaining active connectedNodes : {serverMap.Count}");
        }
    }
}