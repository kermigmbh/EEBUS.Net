using EEBUS.UseCases.ControllableSystem;

namespace EEBUS
{
	public class Settings
	{
		public string		  Certificate { get; set; }
		public DeviceSettings Device      { get; set; }

		public long GetConsumptionNominalMax()
		{
			try
			{
				foreach ( var entity in Device.Entities )
				{
					var usecase = entity.UseCases.FirstOrDefault( uc => uc.Type == "limitationOfPowerConsumption" && uc.Actor == "ControllableSystem" );
					if ( null != usecase )
						return usecase.InitLimits.NominalMax;
				}
			}
			catch
			{
			}

			return 0;
		}

		public long GetProductionNominalMax()
		{
			try
			{
				foreach ( var entity in Device.Entities )
				{
					var usecase = entity.UseCases.FirstOrDefault( uc => uc.Type == "limitationOfPowerProduction" && uc.Actor == "ControllableSystem" );
					if ( null != usecase )
						return usecase.InitLimits.NominalMax;
				}
			}
			catch
			{
			}

			return 0;
		}
	}

	public class DeviceSettings
	{
		public string Name				  { get; set; }
		public string Id				  { get; set; }
		public string Model				  { get; set; }
		public string Brand				  { get; set; }
		public string Type				  { get; set; }
		public string Serial			  { get; set; }
		public string Host				  { get; set; }
		public ushort Port				  { get; set; }
		public string NetworkFeatureSet	  { get; set; }

		public EntitySettings[] Entities  { get; set; } = [];
	}

	public class EntitySettings
	{
		public string			 Type	  { get; set; }

		public UseCaseSettings[] UseCases { get; set; } = [];
	}

	public class UseCaseSettings
	{
		public string		 Type					  { get; set; }
		public string		 Actor					  { get; set; }
		public LimitSettings InitLimits				  { get; set; }
		public int			 PvCurtailmentLimitFactor { get; set; }
	}

	public class LimitSettings
	{
		public bool	Active			 { get; set; }
		public long	Limit			 { get; set; }
		public uint	Duration		 { get; set; }
		public long	FailsafeLimit	 { get; set; }
		public uint	FailsafeDuration { get; set; }
		public long NominalMax		 { get; set; }
	}
}
