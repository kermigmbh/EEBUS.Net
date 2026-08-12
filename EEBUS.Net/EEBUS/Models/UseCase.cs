using EEBUS.SPINE.Commands;

namespace EEBUS.Models
{
    public abstract class UseCase
    {
        protected UseCase(UseCaseSettings usecaseSettings, Entity entity)
        {
            this.Type = usecaseSettings.Type;
            this.Owner = entity;
            this.Scenarios = GetScenarios();

            if (usecaseSettings.SupportedScenarios != null)
            {
                var scenarios = new List<Scenario>(Scenarios);
                foreach (var scenario in scenarios)
                {
                    if (usecaseSettings.SupportedScenarios.Contains(scenario.Index))
                    {
                        Scenarios.Add(scenario);
                    }
                }
            }
            
            Features = GetFeatures(entity).Where(f => f != null).Cast<Feature>();
            foreach (Feature feature in Features)
            {
                entity.GetOrAdd(feature);
            }
        }

        protected abstract List<Scenario> GetScenarios();
        protected abstract IEnumerable<Feature?> GetFeatures(Entity entity);

        /// <summary>
        /// Determines whether the specified feature supports binding as per use case specification
        /// </summary>
        /// <param name="feature"></param>
        /// <returns></returns>
        public virtual bool SupportsBinding(Feature feature)
        {
            return false;
        }

        /// <summary>
        /// Determines whether the specified feature supports subscription as per use case specification
        /// </summary>
        /// <param name="feature"></param>
        /// <returns></returns>
        public virtual bool SupportsSubscription(Feature feature)
        {
            return feature.Role == "server" && feature.Functions.Any(f => f.SupportedFunction.possibleOperations.read != null);
        }

        public abstract class Class
        {
            public abstract UseCase Create(UseCaseSettings usecaseSettings, Entity entity);
        }

        static public UseCase? Create(UseCaseSettings usecaseSettings, Entity entity)
        {
            if (usecasesClasses.TryGetValue(usecaseSettings.Type + "-" + usecaseSettings.Actor, out Class cls))
                return cls.Create(usecaseSettings, entity);

            return null;
        }

        static protected Dictionary<string, Class> usecasesClasses = new Dictionary<string, Class>();

        static protected void Register(string name, Class cls)
        {
            usecasesClasses.Add(name, cls);
        }


        public string Type { get; private set; }

        public List<Scenario> Scenarios = new();
        public IEnumerable<Feature> Features { get; private set; } = [];

        public Entity Owner { get; private set; }

        public abstract string Actor { get; }

        public abstract UseCaseSupportType Information { get; }

        public virtual void FillData<T>(List<T> dataList, Connection connection, Entity entity)
        {
        }
    }
}
