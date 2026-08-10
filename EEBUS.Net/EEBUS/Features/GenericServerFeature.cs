using EEBUS.Models;
using EEBUS.SPINE.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Features
{
    public class GenericServerFeature : Feature
    {
        static GenericServerFeature()
        {
            Register("Generic-server", new Class());
        }

        public GenericServerFeature(Entity owner)
            : base("Generic", "server", owner)
        {
        }

        public GenericServerFeature(int index, Entity owner, FeatureInformationType featureInfo)
            : base(index, "Generic", "server", owner, featureInfo)
        {
        }

        public new class Class : Feature.Class
        {
            public override Feature Create(Entity owner)
            {
                return new GenericServerFeature(owner);
            }

            public override Feature Create(int index, Entity owner, FeatureInformationType featureInfo)
            {
                return new GenericServerFeature(index, owner, featureInfo);
            }
        }

        public override string Description
        {
            get
            {
                return "Generic Server";
            }
        }
    }
}
