using System.Text.Json;
using System.Text.Json.Nodes;

namespace EEBUS.Messages
{
	public class SpineCmdPayload<T> : SpineCmdPayloadBase where T: CmdType, new()
	{
		public SpineCmdPayload()
		{
		}

		public T[] cmd { get; set; } = [new T()];

        public override JsonNode? ToJsonNode()
        {

            var res = JsonSerializer.SerializeToNode(this);
            return res;
        }
    }

	[System.SerializableAttribute()]
	public class CmdType
	{
	}
}
