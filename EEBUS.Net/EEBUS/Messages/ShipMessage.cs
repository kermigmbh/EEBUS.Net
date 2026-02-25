using EEBUS.Enums;
using Makaretu.Dns;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EEBUS.Messages
{
	public abstract class ShipMessage<T> : ShipMessageBase where T: ShipMessage<T>, new()
	{
		public new abstract class Class : ShipMessageBase.Class
		{
		}

		static public ShipMessage<T> template = new T();

		private byte[] sentData;

		protected abstract byte GetDataType();

		static protected void Register( Class cls )
		{
			byte[] test = template.ToJson();
			string cmd = GetCommand( test );

			messages.Add( cmd, cls );
		}

		public override string ToString()
		{
			var options = new JsonSerializerOptions
			{
				Converters = { new JsonStringEnumConverter() }
			};
			return JsonSerializer.Serialize((T)this, options);
		}
		
		protected virtual byte[] ToJson()
		{
			// Serialize this instance to JSON using System.Text.Json
			string json = ToString();
			// Convert into EEBUS-specific JSON structure using string-based helper
			json = JsonIntoEEBUSJson(json);
			return Encoding.UTF8.GetBytes(json);
		}

		public override T FromJsonVirtual(ReadOnlySpan<byte> data/*, Connection connection*/ )
		{
			return FromJson( data/*, connection*/ );
		}

		static public T? FromJson(ReadOnlySpan<byte> data/*, Connection connection*/ )
		{
			if ( data == null || data.Length < 2 )
				return null;

			string dataStr = Encoding.UTF8.GetString( data );
			dataStr = JsonFromEEBUSJson( data[0] == template.GetDataType() ? dataStr.Substring( 1 ) : dataStr );

			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			};
			T obj = JsonSerializer.Deserialize<T>( dataStr, options );
			//obj.connection = connection;

			return obj;
		}

		public override async Task Send( WebSocket ws )
		{
			byte[] msg = ToJson();
			string msgStr = Encoding.Default.GetString( msg );
			this.sentData = new byte[msg.Length + 1];

			this.sentData[0] = GetDataType();
			Buffer.BlockCopy( msg, 0, this.sentData, 1, msg.Length );

            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " ---> " + this.ToString() + "\n");
            await ws.SendAsync( this.sentData, WebSocketMessageType.Binary, true, new CancellationTokenSource( SHIPMessageTimeout.CMI_TIMEOUT ).Token ).ConfigureAwait( false );
		}

		public async Task Resend( WebSocket ws )
		{
            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " re ---> " + this.ToString() + "\n");
            await ws.SendAsync( this.sentData, WebSocketMessageType.Binary, true, new CancellationTokenSource( SHIPMessageTimeout.T_HELLO_PROLONG_WAITING_GAP ).Token ).ConfigureAwait( false );
		}

		static public async Task<T> Receive( WebSocket ws, int timeout = SHIPMessageTimeout.CMI_TIMEOUT )
		{
			byte[] msg = new byte[10240];
			WebSocketReceiveResult result = await ws.ReceiveAsync( msg, new CancellationTokenSource( timeout ).Token ).ConfigureAwait( false );
			if ( result.MessageType == WebSocketMessageType.Close )
				return null;

			if ( (result.Count < 2) || (msg[0] != template.GetDataType()) )
				throw new Exception( $"Expected message of type {template.GetDataType()}!" );

			var ret = template.FromJsonVirtual(msg/*, null*/ );
            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " re <---" + ret.ToString() + "\n");
			return ret;
		}
	}
}
