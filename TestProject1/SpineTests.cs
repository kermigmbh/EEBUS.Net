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
           
            
            var m = ShipMessageBase.Create(Encoding.UTF8.GetBytes(EEBusMessages.MsgNodeManagementDetailedDiscoveryDataReply));

            m = ShipMessageBase.Create(Encoding.UTF8.GetBytes(EEBusMessages.MsgLoadControlLimitListDataWrite));
            
        }
    }
}
