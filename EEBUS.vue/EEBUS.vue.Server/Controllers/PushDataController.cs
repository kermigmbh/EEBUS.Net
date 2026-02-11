using System.Net.WebSockets;
using System.Text;

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using EEBUS.Models;
using EEBUS.UseCases.ControllableSystem;
using System.Collections.Generic;
using EEBUS.UseCases;

namespace EEBUS.Controllers
{
	[Route( "[controller]" )]
	[ApiController]
	public class PushDataController : ControllerBase, IDisposable
	{
		public PushDataController( Devices devices )
		{
			this.devices = devices;

			this.devices.RemoteDeviceFound  += OnRemoteDeviceFound;
			this.devices.ServerStateChanged += OnServerStateChanged;
			this.devices.ClientStateChanged += OnClientStateChanged;

			this.devices.Local.AddUseCaseEvents( this.lpcEventHandler );
			this.devices.Local.AddUseCaseEvents( this.lppEventHandler );
			this.devices.Local.AddUseCaseEvents( this.lpcOrLppEventHandler );
		}

		public void Dispose()
		{
			this.devices.RemoteDeviceFound  -= OnRemoteDeviceFound;
			this.devices.ServerStateChanged -= OnServerStateChanged;
			this.devices.ClientStateChanged -= OnClientStateChanged;

			this.devices.Local.RemoveUseCaseEvents( this.lpcEventHandler );
			this.devices.Local.RemoveUseCaseEvents( this.lppEventHandler );
			this.devices.Local.RemoveUseCaseEvents( this.lpcOrLppEventHandler );
		}

		static private List<WebSocket> webSockets = new List<WebSocket>();

		private Devices				 devices;
		private LPCEventHandler		 lpcEventHandler	  = new();
		private LPPEventHandler		 lppEventHandler	  = new();
		private LPCorLPPEventHandler lpcOrLppEventHandler = new();

		private class LPCEventHandler : LPCEvents
		{
			public WriteApprovalResult ApproveActiveLimitWrite( ActiveLimitWriteRequest request )
			{
				Console.WriteLine( $"LPC Active Limit Write Request: Value={request.Value}, Active={request.IsLimitActive}" );
				return WriteApprovalResult.Deny();
			}

			public WriteApprovalResult ApproveFailsafeLimitWrite( FailsafeLimitWriteRequest request )
			{
				Console.WriteLine( $"LPC Failsafe Limit Write Request: Value={request.Value}" );
				return WriteApprovalResult.Deny();
			}
			
			public void DataUpdateLimit( int counter, bool active, long limit, TimeSpan duration )
			{
				using var _ = Push( new LimitDataChanged( true, active, limit, duration ) );
			}

			public void DataUpdateFailsafeConsumptionActivePowerLimit( int counter, long limit )
			{
				using var _ = Push( new FailsafeLimitDataChanged( true, limit ) );
			}
		}

		private class LPPEventHandler : LPPEvents
		{
			public WriteApprovalResult ApproveActiveLimitWrite( ActiveLimitWriteRequest request )
			{
				Console.WriteLine( $"LPP Active Limit Write Request: Value={request.Value}, Active={request.IsLimitActive}" );
				return WriteApprovalResult.Accept();
			}

			public WriteApprovalResult ApproveFailsafeLimitWrite( FailsafeLimitWriteRequest request )
			{
				Console.WriteLine( $"LPP Failsafe Limit Write Request: Value={request.Value}" );
				return WriteApprovalResult.Accept();
			}
			
			public void DataUpdateLimit( int counter, bool active, long limit, TimeSpan duration )
			{
				using var _ = Push( new LimitDataChanged( false, active, limit, duration ) );
			}

			public void DataUpdateFailsafeProductionActivePowerLimit( int counter, long limit  )
			{
				using var _ = Push( new FailsafeLimitDataChanged( false, limit ) );
			}
		}

		private class LPCorLPPEventHandler : LPCorLPPEvents
		{
			public WriteApprovalResult ApproveFailsafeDurationWrite( FailsafeDurationWriteRequest request )
			{
				Console.WriteLine( $"Failsafe Duration Write Request: Duration={request.Duration}" );
				return WriteApprovalResult.Accept();
			}
			
			public void DataUpdateFailsafeDurationMinimum( int counter, TimeSpan duration )
			{
				using var _ = Push( new FailsafeLimitDurationChanged( duration ) );
			}

			public void DataUpdateHeartbeat( int counter, RemoteDevice device, uint timeout )
			{
				using var _ = Push( new HeartbeatReceived( device, timeout ) );
			}
		}

		private void OnRemoteDeviceFound( RemoteDevice device )
		{
			using var _ = Push( new RemoteDeviceFound( device ) );
		}

		private void OnServerStateChanged( Connection.EState state, RemoteDevice device )
		{
			using var _ = Push( new ServerStateChanged( device, state ) );
		}

		private void OnClientStateChanged( Connection.EState state, RemoteDevice device )
		{
			using var _ = Push( new ClientStateChanged( device, state ) );
		}

		static public bool HasListeners()
		{
			return 0 < webSockets.Count;
		}

		[HttpGet( "/ws/register" )]
		public async Task Register()
		{
			if ( HttpContext.WebSockets.IsWebSocketRequest )
			{
				var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
				webSockets.Add( webSocket );
				Console.WriteLine( "Listener eingetragen" );

				//this.host.ListenerAdded();

				await Echo( HttpContext, webSocket );
			}
			else
			{
				HttpContext.Response.StatusCode = 400;
			}
		}

		private async Task Echo( HttpContext context, WebSocket webSocket )
		{
			var buffer = new byte[1024 * 4];
			WebSocketReceiveResult? result = null;
			try
			{
				result = await webSocket.ReceiveAsync( new ArraySegment<byte>( buffer ), CancellationToken.None );
			}
			catch ( Exception ex )
			{
				webSockets.Remove( webSocket );
				Console.WriteLine( "Listener wieder ausgetragen, Error: " + ex.Message );
				return;
			}

			Console.WriteLine( "Echoschleife Start" );

			while ( ! result.CloseStatus.HasValue )
			{
				string msg = Encoding.UTF8.GetString( buffer ).Substring( 0, result.Count );
				int count = result.Count;

				Console.WriteLine( "Nachricht empfangen: " + msg );

				var responseMsg = Encoding.UTF8.GetBytes( msg );
				Array.Copy( responseMsg, buffer, count );

				await webSocket.SendAsync( new ArraySegment<byte>( buffer, 0, count ), result.MessageType, result.EndOfMessage, CancellationToken.None );
				Console.WriteLine( "Antwort gesendet: " + msg );

				result = await webSocket.ReceiveAsync( new ArraySegment<byte>( buffer ), CancellationToken.None );
			}

			await webSocket.CloseAsync( result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None );

			webSockets.Remove( webSocket );
			Console.WriteLine( "Listener ausgetragen" );
		}

		static public async Task Push( JToken data )
		{
			if ( 0 == webSockets.Count )
				return;

			string json   = data.ToString();
			byte[] buffer = Encoding.UTF8.GetBytes( json );
			int	   count  = buffer.Length;

			foreach ( WebSocket webSocket in webSockets.ToArray() )
			{
				try
				{
					await webSocket.SendAsync( new ArraySegment<byte>( buffer, 0, count ), WebSocketMessageType.Text, true, CancellationToken.None );
				}
				catch ( Exception ex )
				{
					webSockets.Remove( webSocket );
					Console.WriteLine( "Listener ausgetragen, Error: " + ex.Message );
				}
			}
		}
	}
}
