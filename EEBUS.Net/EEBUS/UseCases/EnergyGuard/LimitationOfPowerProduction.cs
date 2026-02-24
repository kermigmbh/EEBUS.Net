using System.Xml;
using EEBUS.DataStructures;
using EEBUS.KeyValues;
using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.UseCases.EnergyGuard
{
    public class LimitationOfPowerProduction : UseCase
    {
        static LimitationOfPowerProduction()
        {
            Register("limitationOfPowerProduction-EnergyGuard", new Class());
        }

        public LimitationOfPowerProduction(UseCaseSettings usecaseSettings, Entity entity)
            : base(usecaseSettings, entity)
        {
            entity.GetOrAdd(Feature.Create("DeviceDiagnosis", "server", entity));
            entity.GetOrAdd(Feature.Create("LoadControl", "client", entity));
            entity.GetOrAdd(Feature.Create("DeviceConfiguration", "client", entity));
            entity.GetOrAdd(Feature.Create("DeviceDiagnosis", "client", entity));
            entity.GetOrAdd(Feature.Create("ElectricalConnection", "client", entity));
        }

        protected override List<Scenario> GetScenarios()
        {
            return [
                new Scenario(1, true, "Control active power production limit"),
                new Scenario(2, true, "Failsafe values"),
                new Scenario(3, true, "Heartbeat"),
                new Scenario(4, true, "Constraints")
            ];
        }

        public new class Class : UseCase.Class
        {
            public override UseCase Create(UseCaseSettings usecaseSettings, Entity entity)
            {
                return new LimitationOfPowerProduction(usecaseSettings, entity);
            }
        }

        public override string Actor { get { return "EnergyGuard"; } }

        public override UseCaseSupportType Information
        {
            get
            {
                List<uint> scenarios = new();
                foreach (var scenario in Scenarios)
                    scenarios.Add(scenario.Index);

                UseCaseSupportType support = new();
                support.useCaseName = "limitationOfPowerProduction";
                support.useCaseVersion = "1.0.0";
                support.useCaseAvailable = true;
                support.scenarioSupport = scenarios.ToArray();
                support.useCaseDocumentSubRevision = "release";

                return support;
            }
        }
    }
}
