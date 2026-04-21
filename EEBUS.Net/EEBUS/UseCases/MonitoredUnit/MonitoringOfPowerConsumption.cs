using System.Data;
using System.Xml;
using EEBUS.DataStructures;
using EEBUS.Features;
using EEBUS.KeyValues;
using EEBUS.Models;
using EEBUS.Net.EEBUS.UseCases.GridConnectionPoint;
using EEBUS.SPINE.Commands;

namespace EEBUS.UseCases.MonitoredUnit
{
	public class MonitoringOfPowerConsumption : UseCase
	{
		static MonitoringOfPowerConsumption()
		{
			Register( "monitoringOfPowerConsumption-MonitoredUnit", new Class() );
		}

		public MonitoringOfPowerConsumption( UseCaseSettings usecaseSettings, Entity entity )
			: base( usecaseSettings, entity )
		{
			MeasurementServerFeature? measurementServer = entity.GetOrAdd( Feature.Create( "Measurement", "server", entity ) ) as MeasurementServerFeature;

            if (measurementServer != null)
            {
                measurementServer.measurementData.Add(new()
                {
                    measurementId = 0,
                    electricalConnectionParameterDescriptionData = new()
                    {
                        measurementId = 0,
                        parameterId = 0,
                        electricalConnectionId = 0,
                        voltageType = "ac",
                        acMeasuredPhases = "abc",
                        acMeasuredInReferenceTo = "neutral",
                        acMeasurementType = "real",
                        acMeasurementVariant = "rms",
                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementId = 0,
                        measurementType = "power",
                        commodityType = "electricity",
                        unit = "W",
                        scopeType = "acPowerTotal"
                    },
                    measurementDataType = new()
                    {
                        measurementId = 0,
                        valueType = "value",
                        value = new() { number = 0, scale = 0 },
                        valueSource = "measuredValue"
                    }
                });

                measurementServer.measurementData.Add(new()
                {
                    measurementId = 1,
                    electricalConnectionParameterDescriptionData = new()
                    {
                        measurementId = 1,
                        parameterId = 1,
                        electricalConnectionId = 0,
                        voltageType = "ac",
                        acMeasurementType = "real",
                        acMeasuredPhases = "a",
                        acMeasuredInReferenceTo = "neutral",
                        acMeasurementVariant = "rms"
                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementId = 1,
                        measurementType = "power",
                        commodityType = "electricity",
                        unit = "W",
                        scopeType = "acPower"
                    },
                    measurementDataType = new()
                    {
                        measurementId = 1,
                        valueType = "value",
                        value = new() { number = 0, scale = 0 },
                        valueSource = "measuredValue"
                    }
                });

                measurementServer.measurementData.Add(new()
                {
                    measurementId = 2,
                    electricalConnectionParameterDescriptionData = new()
                    {
                        measurementId = 2,
                        parameterId = 2,
                        electricalConnectionId = 0,
                        voltageType = "ac",
                        acMeasurementType = "real",
                        acMeasuredPhases = "b",
                        acMeasuredInReferenceTo = "neutral",
                        acMeasurementVariant = "rms"
                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementId = 2,
                        measurementType = "power",
                        commodityType = "electricity",
                        unit = "W",
                        scopeType = "acPower"
                    },
                    measurementDataType = new()
                    {
                        measurementId = 2,
                        valueType = "value",
                        value = new() { number = 0, scale = 0 },
                        valueSource = "measuredValue"
                    }
                });

                measurementServer.measurementData.Add(new()
                {
                    measurementId = 3,
                    electricalConnectionParameterDescriptionData = new()
                    {
                        measurementId = 3,
                        parameterId = 3,
                        electricalConnectionId = 0,
                        voltageType = "ac",
                        acMeasurementType = "real",
                        acMeasuredPhases = "c",
                        acMeasuredInReferenceTo = "neutral",
                        acMeasurementVariant = "rms"
                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementId = 3,
                        measurementType = "power",
                        commodityType = "electricity",
                        unit = "W",
                        scopeType = "acPower"
                    },
                    measurementDataType = new()
                    {
                        measurementId = 3,
                        valueType = "value",
                        value = new() { number = 0, scale = 0 },
                        valueSource = "measuredValue"
                    }
                });

                measurementServer.measurementData.Add(new()
                {
                    measurementId = 4,
                    electricalConnectionParameterDescriptionData = new()
                    {
                        measurementId = 4,
                        parameterId = 4,
                        electricalConnectionId = 0,
                        voltageType = "ac",
                        acMeasurementType = "real",

                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementId = 4,
                        measurementType = "energy",
                        commodityType = "electricity",
                        unit = "Wh",
                        scopeType = "acEnergyConsumed"
                    },
                    measurementDataType = new()
                    {
                        measurementId = 4,
                        valueType = "value",
                        value = new() { number = 0, scale = 0 },
                        valueSource = "measuredValue"
                    }
                });

                measurementServer.measurementData.Add(new()
                {
                    measurementId = 5,
                    electricalConnectionParameterDescriptionData = new()
                    {
                        measurementId = 5,
                        parameterId = 5,
                        electricalConnectionId = 0,
                        voltageType = "ac",
                        acMeasurementType = "real",

                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementId = 5,
                        measurementType = "energy",
                        commodityType = "electricity",
                        unit = "Wh",
                        scopeType = "acEnergyProduced"
                    },
                    measurementDataType = new()
                    {
                        measurementId = 5,
                        valueType = "value",
                        value = new() { number = 0, scale = 0 },
                        valueSource = "measuredValue"
                    }
                });

                measurementServer.measurementData.Add(new()
                {
                    measurementId = 6,
                    electricalConnectionParameterDescriptionData = new()
                    {
                        measurementId = 6,
                        parameterId = 6,
                        electricalConnectionId = 0,
                        voltageType = "ac",
                        acMeasuredPhases = "a",
                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementId = 6,
                        measurementType = "current",
                        commodityType = "electricity",
                        unit = "A",
                        scopeType = "acCurrent"
                    },
                    measurementDataType = new()
                    {
                        measurementId = 6,
                        valueType = "value",
                        value = new() { number = 0, scale = 0 },
                        valueSource = "measuredValue"
                    }
                });

                measurementServer.measurementData.Add(new()
                {
                    measurementId = 7,
                    electricalConnectionParameterDescriptionData = new()
                    {
                        measurementId = 7,
                        parameterId = 7,
                        electricalConnectionId = 0,
                        voltageType = "ac",
                        acMeasuredPhases = "b",
                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementId = 7,
                        measurementType = "current",
                        commodityType = "electricity",
                        unit = "A",
                        scopeType = "acCurrent"
                    },
                    measurementDataType = new()
                    {
                        measurementId = 7,
                        valueType = "value",
                        value = new() { number = 0, scale = 0 },
                        valueSource = "measuredValue"
                    }
                });

                measurementServer.measurementData.Add(new()
                {
                    measurementId = 8,
                    electricalConnectionParameterDescriptionData = new()
                    {
                        measurementId = 8,
                        parameterId = 8,
                        electricalConnectionId = 0,
                        voltageType = "ac",
                        acMeasuredPhases = "c",
                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementId = 8,
                        measurementType = "current",
                        commodityType = "electricity",
                        unit = "A",
                        scopeType = "acCurrent"
                    },
                    measurementDataType = new()
                    {
                        measurementId = 8,
                        valueType = "value",
                        value = new() { number = 0, scale = 0 },
                        valueSource = "measuredValue"
                    }
                });

                measurementServer.measurementData.Add(new()
                {
                    measurementId = 9,
                    electricalConnectionParameterDescriptionData = new()
                    {
                        measurementId = 9,
                        parameterId = 9,
                        electricalConnectionId = 0,
                        voltageType = "ac",
                        acMeasuredPhases = "a",
                        acMeasuredInReferenceTo = "neutral",
                        acMeasurementType = "apparent",
                        acMeasurementVariant = "rms",
                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementId = 9,
                        measurementType = "voltage",
                        commodityType = "electricity",
                        unit = "V",
                        scopeType = "acVoltage"
                    },
                    measurementDataType = new()
                    {
                        measurementId = 9,
                        valueType = "value",
                        value = new() { number = 0, scale = 0 },
                        valueSource = "measuredValue"
                    }
                });

                measurementServer.measurementData.Add(new()
                {
                    measurementId = 10,
                    electricalConnectionParameterDescriptionData = new()
                    {
                        measurementId = 10,
                        parameterId = 10,
                        electricalConnectionId = 0,
                        voltageType = "ac",
                        acMeasuredPhases = "b",
                        acMeasuredInReferenceTo = "neutral",
                        acMeasurementType = "apparent",
                        acMeasurementVariant = "rms",
                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementId = 10,
                        measurementType = "voltage",
                        commodityType = "electricity",
                        unit = "V",
                        scopeType = "acVoltage"
                    },
                    measurementDataType = new()
                    {
                        measurementId = 10,
                        valueType = "value",
                        value = new() { number = 0, scale = 0 },
                        valueSource = "measuredValue"
                    }
                });

                measurementServer.measurementData.Add(new()
                {
                    measurementId = 11,
                    electricalConnectionParameterDescriptionData = new()
                    {
                        measurementId = 11,
                        parameterId = 11,
                        electricalConnectionId = 0,
                        voltageType = "ac",
                        acMeasuredPhases = "c",
                        acMeasuredInReferenceTo = "neutral",
                        acMeasurementType = "apparent",
                        acMeasurementVariant = "rms",
                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementId = 11,
                        measurementType = "voltage",
                        commodityType = "electricity",
                        unit = "V",
                        scopeType = "acVoltage"
                    },
                    measurementDataType = new()
                    {
                        measurementId = 11,
                        valueType = "value",
                        value = new() { number = 0, scale = 0 },
                        valueSource = "measuredValue"
                    }
                });

                measurementServer.measurementData.Add(new()
                {
                    measurementId = 12,
                    electricalConnectionParameterDescriptionData = new()
                    {
                        measurementId = 12,
                        parameterId = 12,
                        electricalConnectionId = 0,
                        voltageType = "ac",
                    },
                    measurementDescriptionDataType = new()
                    {
                        measurementId = 12,
                        measurementType = "frequency",
                        commodityType = "electricity",
                        unit = "Hz",
                        scopeType = "acFrequency"
                    },
                    measurementDataType = new()
                    {
                        measurementId = 12,
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

		public override string Actor { get { return "MonitoredUnit"; } }

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
