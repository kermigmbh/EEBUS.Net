using EEBUS.Models;
using EEBUS.SPINE.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Entities
{
    public class GridConnectionPointOfPremisesEntity : Entity
    {
        static GridConnectionPointOfPremisesEntity()
        {
            Register("GridConnectionPointOfPremises", new Class());
        }

        public GridConnectionPointOfPremisesEntity(int index, LocalDevice local, EntitySettings entitySettings) : base(index, local, entitySettings)
        {
            GetOrAdd(Feature.Create("DeviceConfiguration", "server", this));
            GetOrAdd(Feature.Create("Measurement", "server", this));
            GetOrAdd(Feature.Create("ElectricalConnection", "server", this));
        }

        public GridConnectionPointOfPremisesEntity(int index, LocalDevice local, EntityInformationType entityInfo, FeatureInformationType[] featureInfos) : base(index, local, entityInfo, featureInfos)
        {
        }

        public new class Class : Entity.Class
        {
            public override Entity Create(int index, LocalDevice local, EntitySettings entitySettings)
            {
                return new GridConnectionPointOfPremisesEntity(index, local, entitySettings);
            }

            public override Entity Create(int index, LocalDevice local, EntityInformationType entityInfo, FeatureInformationType[] featureInfos)
            {
                return new GridConnectionPointOfPremisesEntity(index, local, entityInfo, featureInfos);
            }
        }
    }
}
