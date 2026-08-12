using EEBUS.DataStructures;
using EEBUS.KeyValues;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Data.DataStructures;
using EEBUS.SPINE.Commands;
using System.Xml;

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
            if (usecaseSettings.InitLimits != null)
            {
                bool active = usecaseSettings.InitLimits.Active;
                long limit = usecaseSettings.InitLimits.Limit;
                long failsafeLimit = usecaseSettings.InitLimits.FailsafeLimit;

                string xmlDuration = XmlConvert.ToString(usecaseSettings.InitLimits.Duration);
                string xmlFailsafeDuration = XmlConvert.ToString(usecaseSettings.InitLimits.FailsafeDurationMinimum);

                entity.Local.Add(new LoadControlLimitDataStructure("produce", limit, 0, xmlDuration, active));
                entity.Local.Add(new ElectricalConnectionCharacteristicDataStructure("contractualProductionNominalMax", usecaseSettings.InitLimits.NominalMax, 0));

                entity.Local.AddUnique(new FailsafeProductionActivePowerLimitKeyValue(entity.Local, failsafeLimit, 0, true));
                entity.Local.AddUnique(new FailsafeDurationMinimumKeyValue(entity.Local, xmlFailsafeDuration, true));
            }
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

        protected override IEnumerable<Feature?> GetFeatures(Entity entity)
        {
            //See spec for use case lpc
            return [
                Feature.Create("DeviceDiagnosis", "server", entity),
                Feature.Create("LoadControl", "client", entity),
                Feature.Create("DeviceConfiguration", "client", entity),
                Feature.Create("DeviceDiagnosis", "client", entity),
                Feature.Create("ElectricalConnection", "client", entity)
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
