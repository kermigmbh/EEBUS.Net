using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EEBUS.Net
{
    public static class JsonHelper
    {
        private static JsonSerializerOptions s_jsonSerializerOptions = new JsonSerializerOptions()
        {
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public static JsonNode? ToJsonNode<T>(T obj)
        {
            JsonNode? res = JsonSerializer.SerializeToNode(obj, s_jsonSerializerOptions);
            return res;
        }

        public static T? FromJsonNode<T>(JsonNode node)
        {
            T? res = JsonSerializer.Deserialize<T>(node, s_jsonSerializerOptions);
            return res;
        }

        public static string Serialize<T>(T obj)
        {
            string res = JsonSerializer.Serialize(obj, s_jsonSerializerOptions);
            return res;
        }

        public static T Deserialize<T>(string json)
        {
            T? res = JsonSerializer.Deserialize<T>(json, s_jsonSerializerOptions);
            return res;
        }
    }
}
