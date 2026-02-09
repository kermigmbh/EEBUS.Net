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
			public abstract ShipMessageBase Create(ReadOnlySpan<byte> data/*, Connection connection */);
		}

		static protected Dictionary<string, Class> messages = new Dictionary<string, Class>();

		//protected Connection connection;

		public virtual string GetId()
		{
			return null;
		}

		public abstract ShipMessageBase FromJsonVirtual(ReadOnlySpan<byte> data/*, Connection connection */);

		static public ShipMessageBase? Create( ReadOnlySpan<byte> data/*, Connection connection */)
		{
			Class cls = GetClass( data );
			return cls != null ? cls.Create( data/*, connection */) : null;
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

		static protected string GetCommand(ReadOnlySpan<byte> bytes )
		{
			if ( bytes[0] == SHIPMessageType.INIT )
				return "INIT";

			string str = Encoding.UTF8.GetString( bytes );
			int indx1 = str.IndexOf( "{" );
			int indx2 = str.IndexOf( ":" );

			return str.Substring( indx1 + 1, indx2 - indx1 ).Trim( '"' );
		}

		static public Class GetClass( ReadOnlySpan<byte> bytes )
		{
			string cmd = GetCommand( bytes );
			return GetClass( cmd );
		}

		static public Class GetClass( string cmd )
		{
			if ( messages.TryGetValue( cmd, out Class? cls ) )
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

		// helper overload to keep compatibility when callers work with raw JSON strings
		static protected string JsonIntoEEBUSJson( string json )
		{
			JsonNode node = JsonNode.Parse(json)!;
			node = JsonIntoEEBUSJson(node);
			return node.ToJsonString();
		}

		// convert json into the EEBUS json format using System.Text.Json.Nodes
		static protected JsonNode JsonIntoEEBUSJson( JsonNode node )
		{
			if (node is JsonObject obj)
			{
				foreach (var prop in obj.ToList())
				{
					JsonNode? val = prop.Value;
					if (val is JsonObject childObj)
					{
						if (childObj.Any())
						{
							JsonArray replacement = ConvertToArray(childObj);
							if (replacement.Count > 0)
							{
								obj[prop.Key] = replacement;
							}
						}
					}
					else if (val is JsonArray arr)
					{
						for (int i = 0; i < arr.Count; i++)
						{
							if (arr[i] is JsonObject arrayChild && arrayChild.Any())
							{
								JsonArray replacement = ConvertToArray(arrayChild);
								if (replacement.Count > 0)
								{
									arr[i] = replacement;
								}
							}
						}
					}
				}
			}
			return node;
		}
		
		static JsonArray ConvertToArray( JsonObject jo )
		{
			JsonArray replacement = new JsonArray();
			
			var properties = jo.ToList();
			foreach (var p in properties)
			{
				if (p.Key == "datagram" && properties.Count == 1 && p.Value is JsonObject inner)
				{
					JsonObject jp = JsonIntoEEBUSJson(inner) as JsonObject ?? new JsonObject();
					
					JsonArray ja = new JsonArray();
					foreach (var pp in jp)
					{
						JsonObject jpp = new JsonObject
						{
							[pp.Key] = pp.Value?.DeepClone()
                        };
						ja.Add(jpp);
					}
					
					jo[p.Key] = ja;
				}
				else
				{
					JsonObject jp = new JsonObject
					{
						[p.Key] = p.Value?.DeepClone()
					};
					jp = JsonIntoEEBUSJson(jp) as JsonObject ?? jp;
					replacement.Add(jp);
				}
			}
			
			return replacement;
		}
	}
}
