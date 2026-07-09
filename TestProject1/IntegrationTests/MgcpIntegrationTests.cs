using EEBUS;
using EEBUS.Net;
using EEBUS.Net.EEBUS.Models.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TestProject1.IntegrationTests
{
    public class MgcpIntegrationTests : EebusIntegrationTests
    {
        public MgcpIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task Scenario1()
        {
            ILogger eMeterMonitorLogger = GetLogger("E-MeterMonitor");
            ILogger gcpLogger = GetLogger("GridConnectionPoint");

            using EEBUSManager eMeterMonitorManager = new EEBUSManager(Setup.GetEMeterMonitorSettings(), logger: eMeterMonitorLogger);
            using EEBUSManager gcpManager = new EEBUSManager(Setup.GetGridConnectionPointSettings(pvCurtailmentLimitFactor: 100), logger: gcpLogger);

            await StartAndConnectManagersAsync(eMeterMonitorManager, gcpManager);

            var readData = new DeviceData
            {
                Mgcp = new()
            };
            await eMeterMonitorManager.ReadDataAsync(readData, gcpManager.GetLocalData().SKI);

            Assert.Equal(100, readData.Mgcp.PvCurtailmentLimitFactor);
        }

        [Fact]
        public async Task Scenario2To7()
        {
            ILogger eMeterMonitorLogger = GetLogger("E-MeterMonitor");
            ILogger gcpLogger = GetLogger("GridConnectionPoint");

            MeasurementSettings initMeasurements = new MeasurementSettings
            {
                AcPowerTotal = 100,
                GridFeedIn = 50,
                GridConsumption = 150,
                AcCurrentPhaseA = 101,
                AcCurrentPhaseB = 102,
                AcCurrentPhaseC = 103,
                AcVoltagePhaseA = 201,
                AcVoltagePhaseB = 202,
                AcVoltagePhaseC = 203,
                AcFrequency = 230
            };

            using EEBUSManager eMeterMonitorManager = new EEBUSManager(Setup.GetEMeterMonitorSettings(), logger: eMeterMonitorLogger);
            using EEBUSManager gcpManager = new EEBUSManager(Setup.GetGridConnectionPointSettings(initMeasurements), logger: gcpLogger);

            await StartAndConnectManagersAsync(eMeterMonitorManager, gcpManager);

            var data = new DeviceData
            {
                Measurements = new()
            };
            await eMeterMonitorManager.ReadDataAsync(data, gcpManager.GetLocalData().SKI);

            Assert.Equal(initMeasurements.AcPowerTotal, data.Measurements.AcPowerTotal);
            Assert.Equal(initMeasurements.GridFeedIn, data.Measurements.GridFeedIn);
            Assert.Equal(initMeasurements.GridConsumption, data.Measurements.GridConsumption);
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
