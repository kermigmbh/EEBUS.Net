using System.Data;
using System.Xml;
using EEBUS.DataStructures;
using EEBUS.Features;
using EEBUS.KeyValues;
using EEBUS.Models;
using EEBUS.Net.EEBUS.UseCases.GridConnectionPoint;
using EEBUS.SPINE.Commands;

namespace EEBUS.UseCases.MonitoringAppliance
{
	public class MonitoringOfGridConnectionPoint : UseCase
	{
		static MonitoringOfGridConnectionPoint()
		{
			Register( "monitoringOfGridConnectionPoint-MonitoringAppliance", new Class() );
		}

		public MonitoringOfGridConnectionPoint( UseCaseSettings usecaseSettings, Entity entity )
			: base( usecaseSettings, entity )
		{
			entity.GetOrAdd( Feature.Create( "ElectricalConnection", "client", entity ) );

			if (usecaseSettings.PvCurtailmentLimitFactor.HasValue)
			{
				entity.Local.AddUnique(new PvCurtailmentLimitFactorKeyValue(entity.Local, usecaseSettings.PvCurtailmentLimitFactor.Value, 0, true));
			}

			MeasurementClientFeature? measurementClient = entity.GetOrAdd( Feature.Create( "Measurement", "client", entity ) ) as MeasurementClientFeature;

			if (measurementClient != null)
			{
				measurementClient.measurementData.Add(new()
				{
					measurementId = 0,
					electricalConnectionParameterDescriptionData = new()
					{
						electricalConnectionId = 0,
						voltageType = "ac",
						acMeasuredPhases = "abc",
						acMeasuredInReferenceTo = "neutral",
						acMeasurementType = "real",
						acMeasurementVariant = "rms",
					},
					measurementDescriptionDataType = new()
					{
						measurementType = "power",
						commodityType = "electricity",
						unit = "W",
						scopeType = "acPowerTotal"
					},
					measurementDataType = new()
					{
						valueType = "value",
						value = new() { number = 0, scale = 0 },
						valueSource = "measuredValue"
					}
				});

				measurementClient.measurementData.Add(new()
				{
					measurementId = 1,
					electricalConnectionParameterDescriptionData = new()
					{
						electricalConnectionId = 0,
						voltageType = "ac",
						acMeasurementType = "real",
					},
					measurementDescriptionDataType = new()
					{
						measurementType = "energy",
						commodityType = "electricity",
						unit = "Wh",
						scopeType = "gridFeedIn"
					},
					measurementDataType = new()
					{
						valueType = "value",
						value = new() { number = 0, scale = 0 },
						valueSource = "measuredValue"
					}
				});

				measurementClient.measurementData.Add(new()
				{
					measurementId = 2,
					electricalConnectionParameterDescriptionData = new()
					{
						electricalConnectionId = 0,
						voltageType = "ac",
						acMeasurementType = "real",
					},
					measurementDescriptionDataType = new()
					{
						measurementType = "energy",
						commodityType = "electricity",
						unit = "Wh",
						scopeType = "gridConsumption"
					},
					measurementDataType = new()
					{
						valueType = "value",
						value = new() { number = 0, scale = 0 },
						valueSource = "measuredValue"
					}
				});

				measurementClient.measurementData.Add(new()
				{
					measurementId = 3,
					electricalConnectionParameterDescriptionData = new()
					{
						electricalConnectionId = 0,
						voltageType = "ac",
						acMeasuredPhases = "a",
					},
					measurementDescriptionDataType = new()
					{
						measurementType = "current",
						commodityType = "electricity",
						unit = "A",
						scopeType = "acCurrent"
					},
					measurementDataType = new()
					{
						valueType = "value",
						value = new() { number = 0, scale = 0 },
						valueSource = "measuredValue"
					}
				});

				measurementClient.measurementData.Add(new()
				{
					measurementId = 4,
					electricalConnectionParameterDescriptionData = new()
					{
						electricalConnectionId = 0,
						voltageType = "ac",
						acMeasuredPhases = "b",
					},
					measurementDescriptionDataType = new()
					{
						measurementType = "current",
						commodityType = "electricity",
						unit = "A",
						scopeType = "acCurrent"
					},
					measurementDataType = new()
					{
						valueType = "value",
						value = new() { number = 0, scale = 0 },
						valueSource = "measuredValue"
					}
				});

				measurementClient.measurementData.Add(new()
				{
					measurementId = 5,
					electricalConnectionParameterDescriptionData = new()
					{
						electricalConnectionId = 0,
						voltageType = "ac",
						acMeasuredPhases = "c",
					},
					measurementDescriptionDataType = new()
					{
						measurementType = "current",
						commodityType = "electricity",
						unit = "A",
						scopeType = "acCurrent"
					},
					measurementDataType = new()
					{
						valueType = "value",
						value = new() { number = 0, scale = 0 },
						valueSource = "measuredValue"
					}
				});

				measurementClient.measurementData.Add(new()
				{
					measurementId = 6,
					electricalConnectionParameterDescriptionData = new()
					{
						electricalConnectionId = 0,
						voltageType = "ac",
						acMeasuredPhases = "a",
						acMeasuredInReferenceTo = "neutral",
						acMeasurementType = "apparent",
						acMeasurementVariant = "rms",
					},
					measurementDescriptionDataType = new()
					{
						measurementType = "voltage",
						commodityType = "electricity",
						unit = "V",
						scopeType = "acVoltage"
					},
					measurementDataType = new()
					{
						valueType = "value",
						value = new() { number = 0, scale = 0 },
						valueSource = "measuredValue"
					}
				});

				measurementClient.measurementData.Add(new()
				{
					measurementId = 7,
					electricalConnectionParameterDescriptionData = new()
					{
						electricalConnectionId = 0,
						voltageType = "ac",
						acMeasuredPhases = "b",
						acMeasuredInReferenceTo = "neutral",
						acMeasurementType = "apparent",
						acMeasurementVariant = "rms",
					},
					measurementDescriptionDataType = new()
					{
						measurementType = "voltage",
						commodityType = "electricity",
						unit = "V",
						scopeType = "acVoltage"
					},
					measurementDataType = new()
					{
						valueType = "value",
						value = new() { number = 0, scale = 0 },
						valueSource = "measuredValue"
					}
				});

				measurementClient.measurementData.Add(new()
				{
					measurementId = 8,
					electricalConnectionParameterDescriptionData = new()
					{
						electricalConnectionId = 0,
						voltageType = "ac",
						acMeasuredPhases = "c",
						acMeasuredInReferenceTo = "neutral",
						acMeasurementType = "apparent",
						acMeasurementVariant = "rms",
					},
					measurementDescriptionDataType = new()
					{
						measurementType = "voltage",
						commodityType = "electricity",
						unit = "V",
						scopeType = "acVoltage"
					},
					measurementDataType = new()
					{
						valueType = "value",
						value = new() { number = 0, scale = 0 },
						valueSource = "measuredValue"
					}
				});

				measurementClient.measurementData.Add(new()
				{
					measurementId = 9,
					electricalConnectionParameterDescriptionData = new()
					{
						electricalConnectionId = 0,
						voltageType = "ac",
					},
					measurementDescriptionDataType = new()
					{
						measurementType = "frequency",
						commodityType = "electricity",
						unit = "Hz",
						scopeType = "acFrequency"
					},
					measurementDataType = new()
					{
						valueType = "value",
						value = new() { number = 0, scale = 0 },
						valueSource = "measuredValue"
					}
				});
			}
		}

        protected override List<Scenario> GetScenarios()
        {
			return [
				new Scenario(1, true, "Monitor PV feed-in power limitation factor"),
				new Scenario(2, true, "Monitor momentary power consumption/production"),
				new Scenario(3, true, "Monitor total feed-in energy"),
				new Scenario(4, true, "Monitor total consumed energy"),
				new Scenario(5, true, "Monitor momentary current consumption/production phase details"),
				new Scenario(6, true, "Monitor voltage phase details"),
				new Scenario(7, true, "Monitor frequency")
			];
        }

		public new class Class : UseCase.Class
		{
			public override UseCase Create( UseCaseSettings usecaseSettings, Entity entity )
			{
				return new MonitoringOfGridConnectionPoint( usecaseSettings, entity );
			}
		}

		public override string Actor { get { return "MonitoringAppliance"; } }

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
	}
}
