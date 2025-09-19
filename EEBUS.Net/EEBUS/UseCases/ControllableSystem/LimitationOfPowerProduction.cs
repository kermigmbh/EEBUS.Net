using System.Xml;
using EEBUS.DataStructures;
using EEBUS.KeyValues;
using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.UseCases.ControllableSystem
{
	public class LimitationOfPowerProduction : UseCase
	{
		static LimitationOfPowerProduction()
		{
			Register( "limitationOfPowerProduction-ControllableSystem", new Class() );
		}

		public LimitationOfPowerProduction( UseCaseSettings usecaseSettings, Entity entity )
			: base( usecaseSettings, entity )
		{
			Scenarios.Add( new Scenario( 1, true, "Control active power production limit" ) );
			Scenarios.Add( new Scenario( 2, true, "Failsafe values" ) );
			Scenarios.Add( new Scenario( 3, true, "Heartbeat" ) );
			Scenarios.Add( new Scenario( 4, true, "Constraints" ) );

			entity.GetOrAdd( Feature.Create( "DeviceDiagnosis",		 "client", entity ) );
			entity.GetOrAdd( Feature.Create( "LoadControl",			 "server", entity ) );
			entity.GetOrAdd( Feature.Create( "DeviceConfiguration",	 "server", entity ) );
			entity.GetOrAdd( Feature.Create( "DeviceDiagnosis",		 "server", entity ) );
			entity.GetOrAdd( Feature.Create( "ElectricalConnection", "server", entity ) );

			bool active			  = false;
			long limit			  = 0;
			uint duration		  = 0;
			long failsafeLimit	  = 4200;
			uint failsafeDuration = 7200;

			if ( null != usecaseSettings.InitLimits )
			{
				active			 = usecaseSettings.InitLimits.Active;
				limit			 = usecaseSettings.InitLimits.Limit;
				duration		 = usecaseSettings.InitLimits.Duration;
				failsafeLimit	 = usecaseSettings.InitLimits.FailsafeLimit;
				failsafeDuration = usecaseSettings.InitLimits.FailsafeDuration;
			}

			string xmlDuration		   = XmlConvert.ToString( TimeSpan.FromSeconds( duration ) );
			string xmlFailsafeDuration = XmlConvert.ToString( TimeSpan.FromSeconds( failsafeDuration ) );
			
			entity.Local.Add( new LoadControlLimitDataStructure( "produce", limit, 0, xmlDuration, active ) );

			entity.Local.AddUnique( new FailsafeProductionActivePowerLimitKeyValue(	entity.Local, failsafeLimit, 0, true ) );
			entity.Local.AddUnique( new FailsafeDurationMinimumKeyValue(			entity.Local, xmlFailsafeDuration, true ) );
		}

		public new class Class : UseCase.Class
		{
			public override UseCase Create( UseCaseSettings usecaseSettings, Entity entity )
			{
				return new LimitationOfPowerProduction( usecaseSettings, entity );
			}
		}

		public override string Actor { get { return "ControllableSystem"; } }

		public override UseCaseSupportType Information
		{
			get
			{
				List<uint> scenarios = new();
				foreach ( var scenario in Scenarios )
					scenarios.Add( scenario.Index );

				UseCaseSupportType support = new();
				support.useCaseName				   = "limitationOfPowerProduction";
				support.useCaseVersion			   = "1.0.0";
				support.useCaseAvailable		   = true;
				support.scenarioSupport			   = scenarios.ToArray();
				support.useCaseDocumentSubRevision = "release";

				return support;
			}
		}

		public override void FillData<T>( List<T> dataList, Connection connection, Entity entity)
		{
			if ( dataList is not List<ElectricalConnectionCharacteristicDataType> )
				return;

			List<ElectricalConnectionCharacteristicDataType> eccs = dataList as List<ElectricalConnectionCharacteristicDataType>;

			uint id = (uint) eccs.Count;

			ElectricalConnectionCharacteristicDataType ecc = new();
			ecc.electricalConnectionId = 0;
			ecc.parameterId			   = 0;
			ecc.characteristicId	   = id;
			ecc.characteristicContext  = "entity";
			ecc.characteristicType	   = "contractualProductionNominalMax";
			ecc.value.number		   = connection.Local.GetSettings().GetProductionNominalMax();
			ecc.value.scale			   = 0;
			ecc.unit				   = "W";

			eccs.Add( ecc );
		}
	}
}
