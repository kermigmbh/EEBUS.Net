using System.Data;
using System.Xml;
using EEBUS.DataStructures;
using EEBUS.Features;
using EEBUS.KeyValues;
using EEBUS.Models;
using EEBUS.Net.EEBUS.UseCases.GridConnectionPoint;
using EEBUS.SPINE.Commands;

namespace EEBUS.UseCases.GridConnectionPoint
{
	public class MonitoringOfGridConnectionPoint : UseCase
	{
		static MonitoringOfGridConnectionPoint()
		{
			Register( "monitoringOfGridConnectionPoint-GridConnectionPoint", new Class() );
		}

		public MonitoringOfGridConnectionPoint( UseCaseSettings usecaseSettings, Entity entity )
			: base( usecaseSettings, entity )
		{
			Scenarios.Add( new Scenario( 1, true, "Monitor PV feed-in power limitation factor" ) );
			Scenarios.Add( new Scenario( 2, true, "Monitor momentary power consumption/production" ) );
			Scenarios.Add( new Scenario( 3, true, "Monitor total feed-in energy" ) );
			Scenarios.Add( new Scenario( 4, true, "Monitor total consumed energy" ) );
			Scenarios.Add( new Scenario( 5, true, "Monitor momentary current consumption/production phase details" ) );
			Scenarios.Add( new Scenario( 6, true, "Monitor voltage phase details" ) );
			Scenarios.Add( new Scenario( 7, true, "Monitor frequency" ) );

			entity.GetOrAdd( Feature.Create( "ElectricalConnection", "server", entity ) );

			entity.Local.AddUnique( new PvCurtailmentLimitFactorKeyValue( entity.Local, usecaseSettings.PvCurtailmentLimitFactor, 0, true ) );

			MeasurementServerFeature measurementServer = entity.GetOrAdd( Feature.Create( "Measurement", "server", entity ) ) as MeasurementServerFeature;

			measurementServer.measurementData.Add( new()
			{
				measurementId = 0,
				electricalConnectionParameterDescriptionData = new()
				{
					electricalConnectionId	= 0,
					voltageType				= "ac",
					acMeasuredPhases		= "abc",
					acMeasuredInReferenceTo	= "neutral",
					acMeasurementType		= "real",
					acMeasurementVariant	= "rms",
				},
				measurementDescriptionDataType = new()
				{
					measurementType	= "power",
					commodityType	= "electricity",
					unit			= "W",
					scopeType		= "acPowerTotal"
				},
				measurementDataType= new()
				{
					valueType	= "value",
					value		= new() { number = 0, scale = 0 },
					valueSource = "measuredValue"
				}
			} );

			measurementServer.measurementData.Add( new()
			{
				measurementId = 1,
				electricalConnectionParameterDescriptionData = new()
				{
					electricalConnectionId	= 0,
					voltageType				= "ac",
					acMeasurementType		= "real",
				},
				measurementDescriptionDataType = new()
				{
					measurementType	= "energy",
					commodityType	= "electricity",
					unit			= "Wh",
					scopeType		= "gridFeedIn"
				},
				measurementDataType= new()
				{
					valueType	= "value",
					value		= new() { number = 0, scale = 0 },
					valueSource = "measuredValue"
				}
			} );

			measurementServer.measurementData.Add( new()
			{
				measurementId = 2,
				electricalConnectionParameterDescriptionData = new()
				{
					electricalConnectionId	= 0,
					voltageType				= "ac",
					acMeasurementType		= "real",
				},
				measurementDescriptionDataType = new()
				{
					measurementType	= "energy",
					commodityType	= "electricity",
					unit			= "Wh",
					scopeType		= "gridConsumption"
				},
				measurementDataType= new()
				{
					valueType	= "value",
					value		= new() { number = 0, scale = 0 },
					valueSource = "measuredValue"
				}
			} );

			measurementServer.measurementData.Add( new()
			{
				measurementId = 3,
				electricalConnectionParameterDescriptionData = new()
				{
					electricalConnectionId	= 0,
					voltageType				= "ac",
					acMeasuredPhases		= "a",
				},
				measurementDescriptionDataType = new()
				{
					measurementType	= "current",
					commodityType	= "electricity",
					unit			= "A",
					scopeType		= "acCurrent"
				},
				measurementDataType= new()
				{
					valueType	= "value",
					value		= new() { number = 0, scale = 0 },
					valueSource = "measuredValue"
				}
			} );

			measurementServer.measurementData.Add( new()
			{
				measurementId = 4,
				electricalConnectionParameterDescriptionData = new()
				{
					electricalConnectionId	= 0,
					voltageType				= "ac",
					acMeasuredPhases		= "b",
				},
				measurementDescriptionDataType = new()
				{
					measurementType	= "current",
					commodityType	= "electricity",
					unit			= "A",
					scopeType		= "acCurrent"
				},
				measurementDataType= new()
				{
					valueType	= "value",
					value		= new() { number = 0, scale = 0 },
					valueSource = "measuredValue"
				}
			} );

			measurementServer.measurementData.Add( new()
			{
				measurementId = 5,
				electricalConnectionParameterDescriptionData = new()
				{
					electricalConnectionId	= 0,
					voltageType				= "ac",
					acMeasuredPhases		= "c",
				},
				measurementDescriptionDataType = new()
				{
					measurementType	= "current",
					commodityType	= "electricity",
					unit			= "A",
					scopeType		= "acCurrent"
				},
				measurementDataType= new()
				{
					valueType	= "value",
					value		= new() { number = 0, scale = 0 },
					valueSource = "measuredValue"
				}
			} );

			measurementServer.measurementData.Add( new()
			{
				measurementId = 6,
				electricalConnectionParameterDescriptionData = new()
				{
					electricalConnectionId	= 0,
					voltageType				= "ac",
					acMeasuredPhases		= "a",
					acMeasuredInReferenceTo	= "neutral",
					acMeasurementType		= "apparent",
					acMeasurementVariant	= "rms",
				},
				measurementDescriptionDataType = new()
				{
					measurementType	= "voltage",
					commodityType	= "electricity",
					unit			= "V",
					scopeType		= "acVoltage"
				},
				measurementDataType= new()
				{
					valueType	= "value",
					value		= new() { number = 0, scale = 0 },
					valueSource = "measuredValue"
				}
			} );

			measurementServer.measurementData.Add( new()
			{
				measurementId = 7,
				electricalConnectionParameterDescriptionData = new()
				{
					electricalConnectionId	= 0,
					voltageType				= "ac",
					acMeasuredPhases		= "b",
					acMeasuredInReferenceTo	= "neutral",
					acMeasurementType		= "apparent",
					acMeasurementVariant	= "rms",
				},
				measurementDescriptionDataType = new()
				{
					measurementType	= "voltage",
					commodityType	= "electricity",
					unit			= "V",
					scopeType		= "acVoltage"
				},
				measurementDataType= new()
				{
					valueType	= "value",
					value		= new() { number = 0, scale = 0 },
					valueSource = "measuredValue"
				}
			} );

			measurementServer.measurementData.Add( new()
			{
				measurementId = 8,
				electricalConnectionParameterDescriptionData = new()
				{
					electricalConnectionId	= 0,
					voltageType				= "ac",
					acMeasuredPhases		= "c",
					acMeasuredInReferenceTo	= "neutral",
					acMeasurementType		= "apparent",
					acMeasurementVariant	= "rms",
				},
				measurementDescriptionDataType = new()
				{
					measurementType	= "voltage",
					commodityType	= "electricity",
					unit			= "V",
					scopeType		= "acVoltage"
				},
				measurementDataType= new()
				{
					valueType	= "value",
					value		= new() { number = 0, scale = 0 },
					valueSource = "measuredValue"
				}
			} );

			measurementServer.measurementData.Add( new()
			{
				measurementId = 9,
				electricalConnectionParameterDescriptionData = new()
				{
					electricalConnectionId	= 0,
					voltageType				= "ac",
				},
				measurementDescriptionDataType = new()
				{
					measurementType	= "frequency",
					commodityType	= "electricity",
					unit			= "Hz",
					scopeType		= "acFrequency"
				},
				measurementDataType= new()
				{
					valueType	= "value",
					value		= new() { number = 0, scale = 0 },
					valueSource = "measuredValue"
				}
			} );
		}

		public new class Class : UseCase.Class
		{
			public override UseCase Create( UseCaseSettings usecaseSettings, Entity entity )
			{
				return new MonitoringOfGridConnectionPoint( usecaseSettings, entity );
			}
		}

		public override string Actor { get { return "GridConnectionPoint"; } }

		public override UseCaseSupportType Information
		{
			get
			{
				List<uint> scenarios = new();
				foreach ( var scenario in Scenarios )
					scenarios.Add( scenario.Index );

				UseCaseSupportType support = new();
				support.useCaseName				   = "monitoringOfGridConnectionPoint";
				support.useCaseVersion			   = "1.0.0";
				support.useCaseAvailable		   = true;
				support.scenarioSupport			   = scenarios.ToArray();
				support.useCaseDocumentSubRevision = "release";

				return support;
			}
		}
	
		public override void FillData<T>( List<T> dataList, Connection connection, Entity entity )
		{
			if ( dataList is List<ElectricalConnectionParameterDescriptionDataType> )
			{
				List<ElectricalConnectionParameterDescriptionDataType> ecpdds = dataList as List<ElectricalConnectionParameterDescriptionDataType>;

				MeasurementServerFeature feature = entity.Features.Find( f => null != f && f.Type == "Measurement" && f.Role == "server" ) as MeasurementServerFeature;
				foreach ( MeasurementData.MeasurementData data in feature.measurementData )
				{
					data.electricalConnectionParameterDescriptionData.measurementId = data.measurementId;
					ecpdds.Add( data.electricalConnectionParameterDescriptionData );
				}
			}
			else if ( dataList is List<MeasurementDescriptionDataType> )
			{
				List<MeasurementDescriptionDataType> mddts = dataList as List<MeasurementDescriptionDataType>;

				MeasurementServerFeature feature = entity.Features.Find(f => null != f && f.Type == "Measurement" && f.Role == "server") as MeasurementServerFeature;
				foreach ( MeasurementData.MeasurementData data in feature.measurementData )
				{
					data.measurementDescriptionDataType.measurementId = data.measurementId;
					mddts.Add( data.measurementDescriptionDataType );
				}
			}
			else if ( dataList is List<MeasurementDataType> )
			{
				List<MeasurementDataType> mdts = dataList as List<MeasurementDataType>;

				MeasurementServerFeature feature = entity.Features.Find(f => null != f && f.Type == "Measurement" && f.Role == "server") as MeasurementServerFeature;
				foreach ( MeasurementData.MeasurementData data in feature.measurementData )
				{
					data.measurementDataType.measurementId = data.measurementId;
					mdts.Add( data.measurementDataType );
				}
			}
			else if ( dataList is List<MGCPOperationalData> && 1 == dataList.Count )
			{
				MGCPOperationalData data = dataList[0] as MGCPOperationalData;

				MeasurementServerFeature feature = entity.Features.Find( f => null != f && f.Type == "Measurement" && f.Role == "server" ) as MeasurementServerFeature;

				feature.measurementData[0].measurementDataType.value.number = data.PowerTotal;
				feature.measurementData[1].measurementDataType.value.number = data.GridFeedIn;
				feature.measurementData[2].measurementDataType.value.number = data.GridConsumption;
				feature.measurementData[3].measurementDataType.value.number = data.Current[0];
				feature.measurementData[4].measurementDataType.value.number = data.Current[1];
				feature.measurementData[5].measurementDataType.value.number = data.Current[2];
				feature.measurementData[6].measurementDataType.value.number = data.Voltage[0];
				feature.measurementData[7].measurementDataType.value.number = data.Voltage[1];
				feature.measurementData[8].measurementDataType.value.number = data.Voltage[2];
				feature.measurementData[9].measurementDataType.value.number = data.Frequency;
			}
		}
	}
}
