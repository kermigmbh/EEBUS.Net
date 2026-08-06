using EEBUS.Models;
using EEBUS.Net;
using EEBUS.SPINE.Commands;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

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
            var res = JsonHelper.ToJsonNode(this);
            return res;
        }


        public new class Class : SpineCmdPayloadBase.Class
        {
            public override SpineCmdPayload<T>? FromJsonNode(JsonNode? node)
            {
                if (node == null) return null;
                return JsonHelper.FromJsonNode<SpineCmdPayload<T>>(node);
            }
        }
    }

	[System.SerializableAttribute()]
	public class CmdType
	{
	}
}
