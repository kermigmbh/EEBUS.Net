using System.Net.WebSockets;
using System.Diagnostics;

using Microsoft.AspNetCore.Http;

using EEBUS.Models;

namespace EEBUS
{
	public class SHIPMiddleware
	{
		private readonly RequestDelegate next;
		private Devices					 devices;

		public SHIPMiddleware( RequestDelegate next, Devices devices )
		{
			this.next	 = next;
			this.devices = devices;
		}

		public async Task Invoke( HttpContext httpContext )
		{
			try
			{
				if ( ! httpContext.WebSockets.IsWebSocketRequest )
				{
					Debug.WriteLine("Middleware Weiterleitung, kein WebSocket Request");
					// passed on to next middleware
					await this.next( httpContext ).ConfigureAwait( false );
					return;
				}

				if ( ! ProtocolSupported( httpContext ) )
				{
					Debug.WriteLine("Middleware Weiterleitung, kein ship Request");
					// passed on to next middleware
					await this.next( httpContext ).ConfigureAwait( false );
					return;
				}

				Server server = Server.Get( httpContext.Request.Host );
				if (server != null)
				{
					Debug.WriteLine("Middleware Weiterleitung, Server vorhanden und stoppen");
					await server.Close().ConfigureAwait(false);
				}

				var socket = await httpContext.WebSockets.AcceptWebSocketAsync( "ship" ).ConfigureAwait( false );
				if ( socket == null || socket.State != WebSocketState.Open )
				{
					Console.WriteLine( "Failed to accept socket from " + httpContext.Request.Host.ToUriComponent() );
					return;
				}

				server = new Server( httpContext.Request.Host, socket, this.devices );
				await server.Do().ConfigureAwait( false );
			}
			catch ( Exception ex )
			{
				Console.WriteLine( "Exception: " + ex.Message );

				httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
				await httpContext.Response.WriteAsync( "Error while processing websocket: " + ex.Message ).ConfigureAwait( false );
			}
		}

		private bool ProtocolSupported( HttpContext httpContext )
		{
			IList<string> requestedProtocols = httpContext.WebSockets.WebSocketRequestedProtocols;
			return (0 < requestedProtocols.Count) && requestedProtocols.Contains( "ship" );
		}
	}
}
