using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

using EEBUS.Enums;

namespace EEBUS.Messages
{
	public abstract class ShipMessageBase
	{
		public abstract class Class
		{
			public abstract ShipMessageBase Create( byte[] data, Connection connection );
		}

		static protected Dictionary<string, Class> messages = new Dictionary<string, Class>();

		protected Connection connection;

		public virtual string GetId()
		{
			return null;
		}

		public abstract ShipMessageBase FromJsonVirtual( byte[] data, Connection connection );

		static public ShipMessageBase Create( byte[] data, Connection connection )
		{
			Class cls = GetClass( data );
			return cls != null ? cls.Create( data, connection ) : null;
		}

		public virtual (Connection.EState, Connection.ESubState, string) ServerTest( Connection.EState state )
		{
			return (state, Connection.ESubState.None, null);
		}

		public virtual (Connection.EState, Connection.ESubState, string) ClientTest( Connection.EState state )
		{
			return (state, Connection.ESubState.None, null);
		}

		public virtual async Task<(Connection.EState, Connection.ESubState)> NextServerState( Connection connection )
		{
			return (Connection.EState.ErrorOrTimeout, Connection.ESubState.None);
		}

		public virtual async Task<(Connection.EState, Connection.ESubState)> NextClientState( Connection connection )
		{
			return await NextServerState( connection );
		}

		public abstract Task Send( WebSocket ws );

		static protected string GetCommand( byte[] bytes )
		{
			if ( bytes[0] == SHIPMessageType.INIT )
				return "INIT";

			string str = Encoding.UTF8.GetString( bytes );
			int indx1 = str.IndexOf( "{" );
			int indx2 = str.IndexOf( ":" );
			if (indx1 < 0 | indx2 < 0) return "";
			return str.Substring( indx1 + 1, indx2 - indx1 ).Trim( '"' );
		}

		static public Class GetClass( byte[] bytes )
		{
			string cmd = GetCommand( bytes );
			return GetClass( cmd );
		}

		static public Class GetClass( string cmd )
		{
			if ( messages.TryGetValue( cmd, out Class cls ) )
				return cls;

			return null;
		}

		static protected string JsonFromEEBUSJson( string json )
		{
			json = json.Replace( "[{", "{" );
			json = json.Replace( "},{", "," );
			json = json.Replace( "}]", "}" );
			json = json.Replace( "[]", "{}" );

			return json;
		}

		// convert json into the EEBUS json format (Node-based instead of JObject)
		static protected JsonNode JsonIntoEEBUSJson( JsonNode jobj )
		{
			// Existing JObject-based logic has been replaced by a Node-based transformation
			// implemented in ShipMessage<T>.ToJson(); this method is kept only for compatibility.
			return jobj;
		}
	}
}
