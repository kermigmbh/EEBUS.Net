using EEBUS.DataStructures;
using EEBUS.KeyValues;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Data.DataStructures;
using EEBUS.SPINE.Commands;
using System.Collections.Generic;
using System.Xml;

namespace EEBUS.UseCases.ControllableSystem
{
    public class LimitationOfPowerConsumption : UseCase
    {
        static LimitationOfPowerConsumption()
        {
            Register("limitationOfPowerConsumption-ControllableSystem", new Class());
        }

        public LimitationOfPowerConsumption(UseCaseSettings usecaseSettings, Entity entity)
            : base(usecaseSettings, entity)
        {
            if (usecaseSettings.SupportedScenarios != null)
            {
                var scenarios = new List<Scenario>(Scenarios);
                foreach (var scenario in scenarios)
                {
                    if (!usecaseSettings.SupportedScenarios.Contains(scenario.Index))
                    {
                        Scenarios.Remove(scenario);
                    }
                }
            }


            if (usecaseSettings.InitLimits != null)
            {
                bool active = usecaseSettings.InitLimits.Active;
                long limit = usecaseSettings.InitLimits.Limit;
                long failsafeLimit = usecaseSettings.InitLimits.FailsafeLimit;

                string xmlDuration = XmlConvert.ToString(usecaseSettings.InitLimits.Duration);
                string xmlFailsafeDuration = XmlConvert.ToString(usecaseSettings.InitLimits.FailsafeDurationMinimum);

                entity.Local.Add(new LoadControlLimitDataStructure("consume", limit, 0, xmlDuration, active));
                entity.Local.Add(new ElectricalConnectionCharacteristicDataStructure("contractualConsumptionNominalMax", usecaseSettings.InitLimits.NominalMax, 0));

                entity.Local.AddUnique(new FailsafeConsumptionActivePowerLimitKeyValue(entity.Local, failsafeLimit, 0, true));
                entity.Local.AddUnique(new FailsafeDurationMinimumKeyValue(entity.Local, xmlFailsafeDuration, true));
            }
        }

        protected override List<Feature?> GetFeatures(Entity entity)
        {
            //See spec for use case lpc
            return [
                Feature.Create("DeviceDiagnosis", "client", entity),
                Feature.Create("LoadControl", "server", entity),
                Feature.Create("DeviceConfiguration", "server", entity),
                Feature.Create("DeviceDiagnosis", "server", entity),
                Feature.Create("ElectricalConnection", "server", entity)
            ];
        }

        protected override List<Scenario> GetScenarios()
        {
            return [
                new Scenario(1, true, "Control active power consumption limit"),
                new Scenario(2, true, "Failsafe values"),
                new Scenario(3, true, "Heartbeat"),
                new Scenario(4, true, "Constraints")
            ];
        }

        public override bool SupportsBinding(Feature feature)
        {
            //See spec for use case lpc
            return
                (feature.Type == "LoadControl" && feature.Role == "server") ||
                (feature.Type == "DeviceConfiguration" && feature.Role == "server");
        }

        public new class Class : UseCase.Class
        {
            public override UseCase Create(UseCaseSettings usecaseSettings, Entity entity)
            {
                return new LimitationOfPowerConsumption(usecaseSettings, entity);
            }
        }

        public override string Actor { get { return "ControllableSystem"; } }

        public override UseCaseSupportType Information
        {
            get
            {
                List<uint> scenarios = new();
                foreach (var scenario in Scenarios)
                    scenarios.Add(scenario.Index);

                UseCaseSupportType support = new();
                support.useCaseName = "limitationOfPowerConsumption";
                support.useCaseVersion = "1.0.0";
                support.useCaseAvailable = true;
                support.scenarioSupport = scenarios.ToArray();
                support.useCaseDocumentSubRevision = "release";

                return support;
            }
        }
    }
}
