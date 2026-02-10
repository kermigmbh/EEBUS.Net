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
		public Server( HostString host, WebSocket ws, Devices devices )
			: base( host, ws, devices )
		{
			lock (mutex)
			{
				serverMap[host] = this;
			}
		}

		static private ConcurrentDictionary<HostString, Server> serverMap = new();

		static private object mutex = new();
		
		static public Server? Get( HostString host )
		{
			lock ( mutex )
			{
				if ( ! serverMap.TryGetValue( host, out Server server ) )
					return null;

				return server;
			}
		}

		public async Task Do()
		{
			var heart = new HeartBeatTask();
			using var beat = new System.Threading.Timer(heart.Beat, this, 4000, 4000);

			//var ecc        = new ElectricalConnectionCharacteristicTask();
			//using var eccSend   = new System.Threading.Timer( ecc.SendData, this, 2000, Timeout.Infinite );

			//var md         = new MeasurementDataTask();
			//using var mdSend   = new System.Threading.Timer( md.SendData, this, 3000, 3000 );

		 
			try
			{
				while (this.ws.State == WebSocketState.Open)
				{
					 
					using CancellationTokenSource cts = new CancellationTokenSource();
					CancellationToken token = cts.Token;
					var message = await ReceiveAsync(token);

					Debug.WriteLine("<=== " + message.ToString());

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
						RequestRemoteDeviceConfiguration();

					if (this.state == EState.Stopped)
						throw new Exception("Communication stopped!");
				}
			}
			catch (Exception ex)
			{
				if (null != this.Remote)
					this.Remote.SetServerState(EState.Stopped);

				Debug.WriteLine("Exception: " + ex.Message);
			}

			beat.Change(Timeout.Infinite, Timeout.Infinite);
			//eccSend.Change( Timeout.Infinite, Timeout.Infinite );

			await CloseAsync().ConfigureAwait(false);
		}

        public override async Task CloseAsync()
        {
			try
			{
				await this.ws.CloseOutputAsync( WebSocketCloseStatus.NormalClosure, "Closing!", CancellationToken.None ).ConfigureAwait( false );
			}
			catch ( Exception ex )
			{
				Console.WriteLine( "Exception: " + ex.Message );
			}

			serverMap.TryRemove( this.host, out _ );
			this.state = EState.Disconnected;
			this.subState = ESubState.None;
			
            Debug.WriteLine( $"Closed websocket for connectedNode {this.host}. Remaining active connectedNodes : {serverMap.Count}" );
		}
	}
}