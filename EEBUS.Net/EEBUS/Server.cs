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
	/// Represents a server connnection to a remote device that was initiated by a peer's ship node
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
            //Following the spec, we do not send an Init message here, instead we wait for the peer's init message to start the Ship Message Exchange (SME). When we receive the Init message,
            //further processing takes place in the class InitMessage.cs
			Logger?.LogDebug("Starting Server for ski {serverSki}", _ski);

            this.state = EState.WaitingForInit;

            //Initialize the heartbeat timer to send a heartbeat message every 4 seconds. The heartbeat message will be sent to the remote device to keep the connection alive and to check if the remote device is still responsive.
            //Currently, this is only relevant for the use cases LPC and LPP
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
                    var message = await ReceiveWithTimeoutAsync(cancellationToken).ConfigureAwait(false);

                    (this.state, this.subState, string error) = await message.ServerTestAsync(this.state, this).ConfigureAwait(false);

                    if (this.state == EState.Stopped && !string.IsNullOrEmpty(error))
                        throw new Exception(error);
                    if (!string.IsNullOrEmpty(error))
                        Logger?.LogError(error);

                    EState oldState = this.state;
                    (this.state, this.subState) = await message.NextServerStateAsync(this, Logger).ConfigureAwait(false);

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
            catch (TimeoutException ex)
            {
                Logger?.LogWarning(ex.Message);
                await OnReceiveTimeoutAsync().ConfigureAwait(false);
                if (null != this.Remote)
                {
                    this.Remote.SetServerState(EState.Stopped);
                    this.Remote.LastDisconnectUtc = DateTime.UtcNow;
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
                Logger?.LogError(ex, "Server.CloseAsync: CloseOutputAsync failed.");
            }

            // Only remove from the map if we are still the registered Server for
            // this SKI. A newer connection for the same peer may have replaced us
            // already, and in that case we must not remove its entry.

            this.state = EState.Disconnected;
            this.subState = ESubState.None;
        }
    }
}