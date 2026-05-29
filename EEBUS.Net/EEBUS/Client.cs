using EEBUS.Enums;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using Makaretu.Dns;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

namespace EEBUS
{
	public class Client : Connection
	{
		public Client(HostString host, WebSocket ws, Devices devices, RemoteDevice remoteDevice)
			: base(host, ws, devices)
		{
			this.Remote = remoteDevice;
		}

		private Task? _runTask;
		private readonly Lock _runLock = new();

		public Task Run(CancellationToken cancellationToken = default)
		{
			Debug.WriteLine("Running new Client for device " + this.Remote?.Name);

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
						// Observe any exception that escaped RunInternalAsync so it
						// doesn't become an UnobservedTaskException.
						Debug.WriteLine("Client RunInternal escaped exception: " + ex);
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
				Debug.WriteLine("Client.CloseAsync: CloseOutputAsync failed: " + ex.Message);
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
				Debug.WriteLine("Client.CloseAsync: WebSocket dispose failed: " + ex.Message);
			}
		}

		private async Task RunInternalAsync(CancellationToken cancellationToken)
		{
			this.state = EState.Disconnected;
			this.subState = ESubState.None;

			InitMessage initMessage = new InitMessage();

			await initMessage.Send(this.ws).ConfigureAwait(false);

			var heart = new HeartBeatTask();
			Timer beat = new System.Threading.Timer(arg => heart.Beat(arg), this, 4000, 4000);
			//var ecc = new ElectricalConnectionCharacteristicTask();
			//var eccSend = new System.Threading.Timer(ecc.SendData, this, 2000, Timeout.Infinite);

			//var md = new MeasurementDataTask();
			//var mdSend = new System.Threading.Timer(md.SendData, this, 3000, 3000);


			try
			{
				while (this.state != EState.Stopped && !cancellationToken.IsCancellationRequested)
				{

					using CancellationTokenSource timeoutCts = new CancellationTokenSource(SHIPMessageTimeout.CMI_TIMEOUT);
					using CancellationTokenSource linkedTokenSource =
						CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

					var message = await ReceiveAsync(linkedTokenSource.Token);

					(this.state, this.subState, string error) = message.ClientTest(this.state);

					if (this.state == EState.Stopped && error != null)
						throw new Exception(error);
					if (error != null)
						Console.WriteLine(error);

					EState oldState = this.state;
					(this.state, this.subState) = await message.NextClientState(this).ConfigureAwait(false);

					if (null != this.Remote)
						this.Remote.SetClientState(this.state);

					if (this.state == EState.Connected && this.state != oldState && !cancellationToken.IsCancellationRequested)
					{
						RequestRemoteDeviceConfiguration();
						// ReadAndSubscribe();
					}

					ResolvePendingRequest(message);
				}
			}
			catch (Exception ex)
			{
				// consider logging ex
				Debug.WriteLine($"Client connection closed with error: {ex.ToString()}");
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
					Debug.WriteLine("Client.RunInternalAsync: CloseAsync in finally failed: " + ex.Message);
				}
			}
		}
	}
}
