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
                PhaseA = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acCurrent" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "a")?.measurementDataType.value?.number,
                PhaseB = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acCurrent" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "b")?.measurementDataType.value?.number,
                PhaseC = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acCurrent" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "c")?.measurementDataType.value?.number
            };

            AcPhaseData acVoltage = new AcPhaseData
            {
                PhaseA = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acVoltage" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "a")?.measurementDataType.value?.number,
                PhaseB = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acVoltage" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "b")?.measurementDataType.value?.number,
                PhaseC = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acVoltage" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "c")?.measurementDataType.value?.number
            };

            AcPhaseData acPower = new AcPhaseData
            {
                PhaseA = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acPower" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "a")?.measurementDataType.value?.number,
                PhaseB = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acPower" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "b")?.measurementDataType.value?.number,
                PhaseC = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acPower" && data.electricalConnectionParameterDescriptionData.acMeasuredPhases == "c")?.measurementDataType.value?.number
            };

            long? acPowerTotal = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acPowerTotal")?.measurementDataType.value?.number;
            long? gridFeedIn = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "gridFeedIn")?.measurementDataType.value?.number;
            long? gridConsumption = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "gridConsumption")?.measurementDataType.value?.number;
            long? acFrequency = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acFrequency")?.measurementDataType.value?.number;
            long? acEnergyConsumed = measurementData.FirstOrDefault(data => data.measurementDescriptionDataType.scopeType == "acEnergyConsumed")?.measurementDataType.value?.number;

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
    }
}
