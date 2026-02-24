using System.Collections.Generic;
using System.Xml;
using EEBUS.DataStructures;
using EEBUS.KeyValues;
using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.UseCases.EnergyGuard
{
    public class LimitationOfPowerConsumption : UseCase
    {
        static LimitationOfPowerConsumption()
        {
            Register("limitationOfPowerConsumption-EnergyGuard", new Class());
        }

        public LimitationOfPowerConsumption(UseCaseSettings usecaseSettings, Entity entity)
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
                new Scenario(1, true, "Control active power consumption limit"),
                new Scenario(2, true, "Failsafe values"),
                new Scenario(3, true, "Heartbeat"),
                new Scenario(4, true, "Constraints")
            ];
        }

        public new class Class : UseCase.Class
        {
            public override UseCase Create(UseCaseSettings usecaseSettings, Entity entity)
            {
                return new LimitationOfPowerConsumption(usecaseSettings, entity);
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
                support.useCaseName = "limitationOfPowerConsumption";
                support.useCaseVersion = "1.0.0";
                support.useCaseAvailable = true;
                support.scenarioSupport = scenarios.ToArray();
                support.useCaseDocumentSubRevision = "release";

                return support;
            }
        }

        //public override void FillData<T>(List<T> dataList, Connection connection, Entity entity)
        //{
        //    if (dataList is not List<ElectricalConnectionCharacteristicDataType>)
        //        return;

        //    List<ElectricalConnectionCharacteristicDataType> eccs = dataList as List<ElectricalConnectionCharacteristicDataType>;

        //    uint id = (uint)eccs.Count;

        //    ElectricalConnectionCharacteristicDataType ecc = new();
        //    ecc.electricalConnectionId = 0;
        //    ecc.parameterId = 0;
        //    ecc.characteristicId = id;
        //    ecc.characteristicContext = "entity";
        //    ecc.characteristicType = "contractualConsumptionNominalMax";
        //    ecc.value.number = connection.Local.GetSettings().GetConsumptionNominalMax();
        //    ecc.value.scale = 0;
        //    ecc.unit = "W";

        //    eccs.Add(ecc);
        //}
    }
}
