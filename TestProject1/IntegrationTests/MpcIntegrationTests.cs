using EEBUS;
using EEBUS.Net;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace TestProject1.IntegrationTests
{
    public class MpcIntegrationTests : EebusIntegrationTests
    {
        public MpcIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task Scenario1To5()
        {
            ILogger eMeterMonitorLogger = GetLogger("E-MeterMonitor");
            ILogger eMeterLogger = GetLogger("E-Meter");

            MeasurementSettings initMeasurements = new MeasurementSettings
            {
                AcPowerTotal = 100,
                AcPowerPhaseA = 10,
                AcPowerPhaseB = 20,
                AcPowerPhaseC = 30,
                AcEnergyConsumed = 200,
                AcCurrentPhaseA = 101,
                AcCurrentPhaseB = 102,
                AcCurrentPhaseC = 103,
                AcVoltagePhaseA = 201,
                AcVoltagePhaseB = 202,
                AcVoltagePhaseC = 203,
                AcFrequency = 230
            };

            using EEBUSManager eMeterMonitorManager = new EEBUSManager(Setup.GetEMeterMonitorSettings(), logger: eMeterMonitorLogger);
            using EEBUSManager eMeterManager = new EEBUSManager(Setup.GetEMeterSettings(initMeasurements), logger: eMeterLogger);
            string emeterMonitorSki = eMeterMonitorManager.GetLocalData().SKI;
            string emeterSki = eMeterManager.GetLocalData().SKI;

            await StartAndConnectManagersAsync(eMeterMonitorManager, eMeterManager);

            var data = new EEBUS.Net.EEBUS.Models.Data.DeviceData
            {
                Measurements = new()
            };
            await eMeterMonitorManager.ReadDataAsync(data, emeterSki);

            Assert.Equal(initMeasurements.AcPowerTotal, data.Measurements.AcPowerTotal);
            Assert.Equal(initMeasurements.AcPowerPhaseA, data.Measurements.AcPower?.PhaseA);
            Assert.Equal(initMeasurements.AcPowerPhaseB, data.Measurements.AcPower?.PhaseB);
            Assert.Equal(initMeasurements.AcPowerPhaseC, data.Measurements.AcPower?.PhaseC);
            Assert.Equal(initMeasurements.AcEnergyConsumed, data.Measurements.AcEnergyConsumed);
            Assert.Equal(initMeasurements.AcCurrentPhaseA, data.Measurements.AcCurrent?.PhaseA);
            Assert.Equal(initMeasurements.AcCurrentPhaseB, data.Measurements.AcCurrent?.PhaseB);
            Assert.Equal(initMeasurements.AcCurrentPhaseC, data.Measurements.AcCurrent?.PhaseC);
            Assert.Equal(initMeasurements.AcVoltagePhaseA, data.Measurements.AcVoltage?.PhaseA);
            Assert.Equal(initMeasurements.AcVoltagePhaseB, data.Measurements.AcVoltage?.PhaseB);
            Assert.Equal(initMeasurements.AcVoltagePhaseC, data.Measurements.AcVoltage?.PhaseC);
            Assert.Equal(initMeasurements.AcFrequency, data.Measurements.AcFrequency);
        }
    }
}
