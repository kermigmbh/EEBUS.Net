using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;

using Microsoft.AspNetCore.Http;

using EEBUS.Messages;
using EEBUS.Models;
using Microsoft.Extensions.Logging;

namespace EEBUS
{
    /// <summary>
    /// Eine Server Instanz verwaltet eine Websocket Instanz, die mit einem wss:// Aufruf von einem anderen Device entstanden ist
    /// Es wird auf ankommende Nachrichten gewartet und meist mit Echo darauf reagiert
    /// </summary>
    public class Server : Connection
    {
        private readonly string _ski;

        public Server(string ski, HostString host, WebSocket ws, Devices devices, ILogger? logger = null)
            : base(host, ws, devices, logger)
        {
            this._ski = ski ?? string.Empty;
            this.Remote = devices.GetRemotes().FirstOrDefault(r => r.SKI.ToString() == ski);
        }

        public async Task Do(CancellationToken cancellationToken)
        {
			Logger?.LogDebug("Starting Server for ski {serverSki}", _ski);
            var heart = new HeartBeatTask();
            Timer beat = new System.Threading.Timer(arg => heart.Beat(arg), this, 4000, 4000);

            if (this.Remote != null)
            {
                this.Remote.LastDisconnectUtc = null;
            }
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
                    (this.state, this.subState) = await message.NextServerState(this, Logger).ConfigureAwait(false);

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

                Logger?.LogError(ex, "Error during Server.Do");
            }
            finally
            {
                // Dispose the timer (Change(Infinite, Infinite) only stops further
                // callbacks; the underlying TimerQueueTimer is only released by Dispose).
                try { beat.Dispose(); } catch { }
            }

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

            this.state = EState.Disconnected;
            this.subState = ESubState.None;
        }
    }
}