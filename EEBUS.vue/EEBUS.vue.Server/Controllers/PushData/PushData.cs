using System.Text.Json.Nodes;

namespace EEBUS.Controllers
{
	public class PushData
	{
		public JsonObject Payload { get; } = new JsonObject();

		public PushData( string cmd )
		{
			Payload["cmd"] = cmd;
		}

		protected void AddData( object data )
		{
			// serialize arbitrary data object into a JsonNode for transport
			var node = System.Text.Json.JsonSerializer.SerializeToNode( data );
			Payload["data"] = node;
		}
	}
}
