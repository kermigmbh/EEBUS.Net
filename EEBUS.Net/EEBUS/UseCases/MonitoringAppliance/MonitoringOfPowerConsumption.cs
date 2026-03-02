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
	public class MonitoringOfPowerConsumption : UseCase
	{
		static MonitoringOfPowerConsumption()
		{
			Register( "monitoringOfPowerConsumption-MonitoringAppliance", new Class() );
		}

		public MonitoringOfPowerConsumption( UseCaseSettings usecaseSettings, Entity entity )
			: base( usecaseSettings, entity )
		{
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
						acMeasuredPhases = "a",
						acMeasuredInReferenceTo = "neutral",
						acMeasurementVariant = "rms"
					},
					measurementDescriptionDataType = new()
					{
						measurementType = "power",
						commodityType = "electricity",
						unit = "W",
						scopeType = "acPower"
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
                        acMeasuredPhases = "b",
                        acMeasuredInReferenceTo = "neutral",
                        acMeasurementVariant = "rms"
                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementType = "power",
                        commodityType = "electricity",
                        unit = "W",
                        scopeType = "acPower"
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
                        acMeasurementType = "real",
                        acMeasuredPhases = "c",
                        acMeasuredInReferenceTo = "neutral",
                        acMeasurementVariant = "rms"
                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementType = "power",
                        commodityType = "electricity",
                        unit = "W",
                        scopeType = "acPower"
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
						acMeasurementType = "real",
						
					},
					measurementDescriptionDataType = new()
					{
						measurementType = "energy",
						commodityType = "electricity",
						unit = "Wh",
						scopeType = "acEnergyConsumed"
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
					measurementId = 6,
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
					measurementId = 7,
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
					measurementId = 8,
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
					measurementId = 9,
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
					measurementId = 10,
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
					measurementId = 11,
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
				new Scenario(1, true, "Monitor power"),
				new Scenario(2, true, "Monitor current"),
				new Scenario(3, true, "Monitor energy"),
				new Scenario(4, true, "Monitor voltage"),
				new Scenario(5, true, "Monitor frequency"),
			];
        }

		public new class Class : UseCase.Class
		{
			public override UseCase Create( UseCaseSettings usecaseSettings, Entity entity )
			{
				return new MonitoringOfPowerConsumption( usecaseSettings, entity );
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
				support.useCaseName				   = "monitoringOfPowerConsumption";
				support.useCaseVersion			   = "1.0.0";
				support.useCaseAvailable		   = true;
				support.scenarioSupport			   = scenarios.ToArray();
				support.useCaseDocumentSubRevision = "release";

				return support;
			}
		}
	}
}
