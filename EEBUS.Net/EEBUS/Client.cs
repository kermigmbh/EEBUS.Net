using EEBUS.Enums;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using Makaretu.Dns;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

namespace EEBUS
{
	/// <summary>
	/// Represents a client connnection to a remote device that was initiated by our ship node
	/// </summary>
	public class Client : Connection
	{
		public Client(HostString host, WebSocket ws, Devices devices, RemoteDevice remoteDevice, ILogger? logger = null)
			: base(host, ws, devices, logger)
		{
			this.Remote = remoteDevice;
		}

		private Task? _runTask;
		private readonly Lock _runLock = new();

		public Task Run(CancellationToken cancellationToken = default)
		{
			Logger?.LogDebug("Running new Client for device " + this.Remote?.Name);

			lock (_runLock)
			{
				if (_runTask != null)
				{
					// Already started; return the existing task so callers awaiting
					// a second Run() observe the same lifetime instead of starting
					// a parallel receive loop on the same socket.
					return Task.CompletedTask;
				}

				_runTask = Task.Run(async () =>
				{
					try
					{
						await RunInternalAsync(cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
                        Logger?.LogError(ex, "Client RunInternal escaped exception.");
						// Observe any exception that escaped RunInternalAsync so it
						// doesn't become an UnobservedTaskException.
					}
				});
			}

			// Return immediately; callers (EEBUSManager.ConnectAsync, reconnect loop)
			// are not expected to block for the lifetime of the connection.
			return Task.CompletedTask;
		}

		/// <summary>
		/// Awaits the background receive loop, if one was started. Useful for
		/// orderly shutdown paths that want to ensure the worker actually exited.
		/// </summary>
		public Task WaitForCompletionAsync()
		{
			lock (_runLock)
			{
				return _runTask ?? Task.CompletedTask;
			}
		}

		public override async Task CloseAsync()
		{
			// Best-effort: send a clean WebSocket close. The receive loop will then
			// see the closure, exit, and the finally block in RunInternalAsync will
			// run timer disposal etc.
			try
			{
				if (this.ws.State == WebSocketState.Open || this.ws.State == WebSocketState.CloseReceived)
				{
					await this.ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing!", CancellationToken.None).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
               Logger?.LogError(ex, "Client.CloseAsync: CloseOutputAsync failed.");
			}

			// Dispose the underlying socket. The Client took ownership of it from
			// EEBUSManager.ConnectAsync (see the "Ownership transferred" comment
			// there); nothing else releases it.
			try
			{
				this.ws.Dispose();
			}
			catch (Exception ex)
			{
              Logger?.LogError(ex, "Client.CloseAsync: WebSocket dispose failed.");
			}
		}

		private async Task RunInternalAsync(CancellationToken cancellationToken)
		{
			this.state = EState.Disconnected;
			this.subState = ESubState.None;

            //Start the Ship Message Exchange (SME) by sending the InitMessage to the remote device. Once the SME is finished, the connection state will be set to Connected and the heartbeat timer will be started.
            InitMessage initMessage = new InitMessage();
			await initMessage.Send(this.ws, Logger).ConfigureAwait(false);

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
				//Main receive loop. When the peer sends a message to us, it gets processed here.
				while (this.state != EState.Stopped && !cancellationToken.IsCancellationRequested)
				{
					var message = await ReceiveWithTimeoutAsync(cancellationToken).ConfigureAwait(false);

					(this.state, this.subState, string error) = await message.ClientTestAsync(this.state, this, Logger);

					if (this.state == EState.Stopped && !string.IsNullOrEmpty(error))
						throw new Exception(error);
					if (!string.IsNullOrEmpty(error))
						Logger?.LogError(error);

					EState oldState = this.state;
					(this.state, this.subState) = await message.NextClientStateAsync(this, Logger).ConfigureAwait(false);

					if (null != this.Remote)
						this.Remote.SetClientState(this.state);

					if (this.state == EState.Connected && this.state != oldState && !cancellationToken.IsCancellationRequested)
					{
						//Now that the SME handshake is complete, we send a NodeManagementDetailedDiscovery read message to understand the peer's node structure (entities, features, functions)
						RequestRemoteDeviceConfiguration();
					}

					ResolvePendingRequest(message);
				}
			}
			catch (TimeoutException ex)
			{
				Logger?.LogWarning(ex.Message);
				await OnReceiveTimeoutAsync().ConfigureAwait(false);
				if (this.Remote != null)
				{
					this.Remote.LastDisconnectUtc = DateTime.UtcNow;
				}
			}
			catch (Exception ex)
			{
			  Logger?.LogError(ex, "Client connection closed with error.");
				this.state = EState.ErrorOrTimeout;
				if (this.Remote != null)
				{
					this.Remote.LastDisconnectUtc = DateTime.UtcNow;
				}
			}
			finally
			{
				// Dispose the timer (Change(Infinite, Infinite) only stops further
				// callbacks; the underlying TimerQueueTimer is only released by Dispose).
				try { beat.Dispose(); } catch { }

				if (null != this.Remote)
					this.Remote.SetClientState(EState.Stopped);

				// Make sure the socket is released even if no one calls CloseAsync.
				try
				{
					await CloseAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
                    Logger?.LogError(ex, "Client.RunInternalAsync: CloseAsync in finally failed.");
				}
			}
		}
	}
}
