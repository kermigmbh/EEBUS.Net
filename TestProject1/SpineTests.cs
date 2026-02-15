using EEBUS;
using EEBUS.Messages;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace TestProject1
{
    public class SpineTests
    {
        private static Type[] GetTypesInNamespace(Assembly assembly, string nameSpace)
        {
            return assembly.GetTypes()
                            .Where(t => String.Equals(t.Namespace, nameSpace, StringComparison.Ordinal))
                            .ToArray();
        }
        public SpineTests()
        {
            foreach (string ns in new string[] {"EEBUS.SHIP.Messages", "EEBUS.SPINE.Commands", "EEBUS.Entities",
                                                 "EEBUS.UseCases.ControllableSystem", "EEBUS.UseCases.GridConnectionPoint",
                                                 "EEBUS.Features" })
            {
                foreach (Type type in GetTypesInNamespace(typeof(Settings).Assembly, ns))
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
        }

        

        [Fact]
        public void Test1()
        {
            string spineMsg = """
{
  "header": {
    "msgId": "4711",
    "timestamp": "2026-02-15T14:45:00Z",
    "source": "evse_01",
    "destination": "energy_manager"
  },
  "body": {
    "device": {
      "features": [
        {
          "featureId": 3,
          "featureType": "loadControl",
          "functions": [
            {
              "function": "loadControlLimit",
              "cmd": "write",
              "data": [
                {
                  "limitId": 1,
                  "value": 3500,
                  "isActive": true
                }
              ]
            }
          ]
        }
      ]
    }
  }
}
""";
            DataMessage limitMessage = new DataMessage();
            SpineDatagramPayload notify = new SpineDatagramPayload();
            limitMessage.SetPayload(System.Text.Json.JsonSerializer.SerializeToNode(notify));
            var m = ShipMessageBase.Create(Encoding.UTF8.GetBytes(spineMsg));
            


        }
    }
}
