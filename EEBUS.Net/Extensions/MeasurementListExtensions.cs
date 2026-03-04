using EEBUS.Net.EEBUS.Models.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.Extensions
{
    public static class MeasurementListExtensions
    {
        public static MeasurementsData CollectData(this List<MeasurementData.MeasurementData> measurementData)
        {
            AcPhaseData acCurrent = new AcPhaseData
            {
                PhaseA = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acCurrent" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "a")?.MeasuredValue,
                PhaseB = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acCurrent" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "b")?.MeasuredValue,
                PhaseC = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acCurrent" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "c")?.MeasuredValue
            };

            AcPhaseData acVoltage = new AcPhaseData
            {
                PhaseA = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acVoltage" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "a")?.MeasuredValue,
                PhaseB = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acVoltage" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "b")?.MeasuredValue,
                PhaseC = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acVoltage" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "c")?.MeasuredValue
            };

            AcPhaseData acPower = new AcPhaseData
            {
                PhaseA = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acPower" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "a")?.MeasuredValue,
                PhaseB = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acPower" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "b")?.MeasuredValue,
                PhaseC = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acPower" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "c")?.MeasuredValue
            };

            long? acPowerTotal = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acPowerTotal")?.MeasuredValue;
            long? gridFeedIn = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "gridFeedIn")?.MeasuredValue;
            long? gridConsumption = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "gridConsumption")?.MeasuredValue;
            long? acFrequency = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acFrequency")?.MeasuredValue;
            long? acEnergyConsumed = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acEnergyConsumed")?.MeasuredValue;

            return new MeasurementsData
            {
                AcPowerTotal = acPowerTotal,
                GridFeedIn = gridFeedIn,
                GridConsumption = gridConsumption,
                AcPower = acPower,
                AcEnergyConsumed = acEnergyConsumed,
                AcCurrent = acCurrent,
                AcVoltage = acVoltage,
                AcFrequency = acFrequency
            };
        }

        public static void Update(this List<MeasurementData.MeasurementData> measurementData, MeasurementsData data)
        {
            measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acPowerTotal")?.UpdateValue(data.AcPowerTotal);
            measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "gridFeedIn")?.UpdateValue(data.GridFeedIn);
            measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "gridConsumption")?.UpdateValue(data.GridConsumption);
            measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acPower" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "a")?.UpdateValue(data.AcPower?.PhaseA);
            measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acPower" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "b")?.UpdateValue(data.AcPower?.PhaseB);
            measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acPower" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "c")?.UpdateValue(data.AcPower?.PhaseC);
            measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acEnergyConsumed")?.UpdateValue(data.AcEnergyConsumed);
            measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acCurrent" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "a")?.UpdateValue(data.AcCurrent?.PhaseA);
            measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acCurrent" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "b")?.UpdateValue(data.AcCurrent?.PhaseB);
            measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acCurrent" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "c")?.UpdateValue(data.AcCurrent?.PhaseC);
            measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acVoltage" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "a")?.UpdateValue(data.AcVoltage?.PhaseA);
            measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acVoltage" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "b")?.UpdateValue(data.AcVoltage?.PhaseB);
            measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acVoltage" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "c")?.UpdateValue(data.AcVoltage?.PhaseC);
            measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acFrequency")?.UpdateValue(data.AcFrequency);
        }
    }
}
