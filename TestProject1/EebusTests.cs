using EEBUS;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace TestProject1
{
    public class EebusTests
    {
        protected const string DefaultRemoteSki = "c09ff4c4dc2916414714662366f968f4743af7b7";
        protected const string DefaultLocalSki = "662728a479fa2fcf28e6d9e7855e996ab1d850a2";

        public EebusTests()
        {
            foreach (string ns in new string[] {"EEBUS.SHIP.Messages", "EEBUS.SPINE.Commands", "EEBUS.Entities",
                                                 "EEBUS.UseCases.ControllableSystem", "EEBUS.UseCases.EnergyGuard", "EEBUS.UseCases.GridConnectionPoint", "EEBUS.UseCases.MonitoringAppliance",
                                                 "EEBUS.UseCases.MonitoredUnit", "EEBUS.Features" })
            {
                foreach (Type type in GetTypesInNamespace(typeof(Settings).Assembly, ns))
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
        }

        private Type[] GetTypesInNamespace(Assembly assembly, string nameSpace)
        {
            return assembly.GetTypes()
                            .Where(t => String.Equals(t.Namespace, nameSpace, StringComparison.Ordinal))
                            .ToArray();
        }

        protected byte[] GetSkiBytes(string ski)
        {
            return Enumerable.Range(0, ski.Length / 2)
                                 .Select(x => Convert.ToByte(ski.Substring(x * 2, 2), 16))
                                 .ToArray();
        }
    }
}
