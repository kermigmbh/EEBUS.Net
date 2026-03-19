using EEBUS.Models;
using EEBUS.SPINE.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Entities
{
    public class GenericEntity : Entity
    {
        static GenericEntity()
        {
            Register("Generic", new Class());
        }

        public GenericEntity(int index, LocalDevice local, EntitySettings entitySettings) : base(index, local, entitySettings)
        {
        }

        public GenericEntity(int index, LocalDevice local, EntityInformationType entityInfo, FeatureInformationType[] featureInfos) : base(index, local, entityInfo, featureInfos)
        {
        }

        public new class Class : Entity.Class
        {
            public override Entity Create(int index, LocalDevice local, EntitySettings entitySettings)
            {
                return new GenericEntity(index, local, entitySettings);
            }

            public override Entity Create(int index, LocalDevice local, EntityInformationType entityInfo, FeatureInformationType[] featureInfos)
            {
                return new GenericEntity(index, local, entityInfo, featureInfos);
            }
        }
    }
}
