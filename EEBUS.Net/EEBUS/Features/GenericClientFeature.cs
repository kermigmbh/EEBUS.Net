using EEBUS.Features;
using EEBUS.Models;
using EEBUS.SPINE.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Features
{
    public class GenericClientFeature : Feature
    {
        static GenericClientFeature()
        {
            Register("Generic-client", new Class());
        }

        public GenericClientFeature(Entity owner)
            : base("Generic", "client", owner)
        {
        }

        public GenericClientFeature(int index, Entity owner, FeatureInformationType featureInfo)
            : base(index, "Generic", "client", owner, featureInfo)
        {
        }

        public new class Class : Feature.Class
        {
            public override Feature Create(Entity owner)
            {
                return new GenericClientFeature(owner);
            }

            public override Feature Create(int index, Entity owner, FeatureInformationType featureInfo)
            {
                return new GenericClientFeature(index, owner, featureInfo);
            }
        }

        public override string Description
        {
            get
            {
                return "Generic Client";
            }
        }
    }
}
