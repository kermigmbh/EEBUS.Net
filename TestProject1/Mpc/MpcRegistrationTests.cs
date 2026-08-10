using System.Reflection;
using System.Runtime.CompilerServices;
using EEBUS;
using EEBUS.Messages;
using EEBUS.Models;

namespace TestProject1.Mpc
{
    /// <summary>
    /// Unit-Tests für die spezifikationskonforme Feature-Registrierung des
    /// "Monitoring of Power Consumption" (MPC) Use Cases.
    /// </summary>
    public class MpcRegistrationTests : EebusTests
    {
        private const string TestLocalSki = "662728a479fa2fcf28e6d9e7855e996ab1d850a2";
        private const string TestRemoteSki = "c09ff4c4dc2916414714662366f968f4743af7b7";

        protected override DeviceSettings GetDeviceSettings()
        {
            return new DeviceSettings
            {
                Name = "TestMPCDevice",
                Id = "Test-MPC-Device",
                Model = "TestModel",
                Brand = "TestBrand",
                Type = "EnergyManagementSystem",
                Serial = "MPC001",
                Port = 7202,
                Entities =
                [
                    new EntitySettings { Type = "DeviceInformation" },
                    new EntitySettings
                    {
                        Type = "CEM",
                        UseCases =
                        [
                            new UseCaseSettings
                            {
                                Type = "monitoringOfPowerConsumption",
                                Actor = "MonitoredUnit",
                            },
                        ],
                    },
                ],
            };
        }

        /// <summary>
        /// Gibt den Featurenamen eines Function-Objekts zurück.
        /// (Function.name ist privat; der Weg führt über SupportedFunction.)
        /// </summary>
        private static string GetFunctionName(Function f) => f.SupportedFunction.function;

        // ══════════════════════════════════════════════════════════════════════════
        // MonitoredUnit – Feature-Registrierung
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// EEBUS-SPINE-UC-MPC: Der MonitoredUnit-Actor MUSS ein Measurement-server-Feature
        /// bereitstellen, über das Leistungs-, Strom-, Spannungs-, Energie- und
        /// Frequenzwerte abgerufen werden können.
        /// </summary>
        [Fact]
        public void MpcMonitoredUnit_RegistersMeasurementServerFeature()
        {
            Connection connection = GetMockConnection(TestLocalSki, TestRemoteSki);

            AddressType? address = connection.Local.GetFeatureAddress("Measurement", server: true);

            Assert.NotNull(address);
        }

        /// <summary>
        /// EEBUS-SPINE-UC-MPC: Der MonitoredUnit-Actor MUSS ein ElectricalConnection-server-Feature
        /// bereitstellen, damit der MonitoringAppliance-Actor über
        /// electricalConnectionParameterDescriptionListData die Messpunkte den
        /// elektrischen Anschlüssen/Phasen zuordnen kann.
        /// </summary>
        [Fact]
        public void MpcMonitoredUnit_RegistersElectricalConnectionServerFeature()
        {
            Connection connection = GetMockConnection(TestLocalSki, TestRemoteSki);

            AddressType? address = connection.Local.GetFeatureAddress("ElectricalConnection", server: true);

            Assert.NotNull(address);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // MonitoredUnit – Measurement-Feature-Funktionen (Pflichtfunktionen laut Spec)
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// EEBUS-SPINE-UC-MPC §5.x: Das Measurement-server-Feature MUSS
        /// measurementDescriptionListData exponieren.
        /// </summary>
        [Fact]
        public void MpcMonitoredUnit_MeasurementFeature_ExposesDescriptionListFunction()
        {
            Connection connection = GetMockConnection(TestLocalSki, TestRemoteSki);

            Feature? feature = connection.Local.Entities
                .SelectMany(e => e.Features)
                .FirstOrDefault(f => f.Type == "Measurement" && f.Role == "server");

            Assert.NotNull(feature);
            Assert.Contains(feature.Functions, f => GetFunctionName(f) == "measurementDescriptionListData");
        }

        /// <summary>
        /// EEBUS-SPINE-UC-MPC §5.x: Das Measurement-server-Feature MUSS
        /// measurementListData exponieren.
        /// </summary>
        [Fact]
        public void MpcMonitoredUnit_MeasurementFeature_ExposesMeasurementListFunction()
        {
            Connection connection = GetMockConnection(TestLocalSki, TestRemoteSki);

            Feature? feature = connection.Local.Entities
                .SelectMany(e => e.Features)
                .FirstOrDefault(f => f.Type == "Measurement" && f.Role == "server");

            Assert.NotNull(feature);
            Assert.Contains(feature.Functions, f => GetFunctionName(f) == "measurementListData");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // MonitoredUnit – ElectricalConnection-Feature-Funktionen
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// EEBUS-SPINE-UC-MPC §5.x: Das ElectricalConnection-server-Feature MUSS
        /// electricalConnectionParameterDescriptionListData exponieren, damit der
        /// MonitoringAppliance-Actor Messpunkte (measurementId) den elektrischen
        /// Parametern (Phase, Anschluss) zuordnen kann.
        /// </summary>
        [Fact]
        public void MpcMonitoredUnit_ElectricalConnectionFeature_ExposesParameterDescriptionListFunction()
        {
            Connection connection = GetMockConnection(TestLocalSki, TestRemoteSki);

            Feature? feature = connection.Local.Entities
                .SelectMany(e => e.Features)
                .FirstOrDefault(f => f.Type == "ElectricalConnection" && f.Role == "server");

            Assert.NotNull(feature);
            Assert.Contains(
                feature.Functions,
                f => GetFunctionName(f) == "electricalConnectionParameterDescriptionListData");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // MonitoringAppliance – Feature-Registrierung
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// EEBUS-SPINE-UC-MPC: Der MonitoringAppliance-Actor MUSS ein
        /// Measurement-client-Feature haben, um Messdaten vom MonitoredUnit
        /// abonnieren und lesen zu können.
        /// </summary>
        [Fact]
        public void MpcMonitoringAppliance_RegistersMeasurementClientFeature()
        {
            Connection connection = GetMockConnection(TestLocalSki, TestRemoteSki);

            AddressType? address = connection.Local.GetFeatureAddress("Measurement", server: false);

            Assert.NotNull(address);
        }

        /// <summary>
        /// EEBUS-SPINE-UC-MPC: Der MonitoringAppliance-Actor MUSS ein
        /// ElectricalConnection-client-Feature haben, um
        /// electricalConnectionParameterDescriptionListData vom MonitoredUnit lesen
        /// zu können.
        /// </summary>
        [Fact]
        public void MpcMonitoringAppliance_RegistersElectricalConnectionClientFeature()
        {
            Connection connection = GetMockConnection(TestLocalSki, TestRemoteSki);

            AddressType? address = connection.Local.GetFeatureAddress("ElectricalConnection", server: false);

            Assert.NotNull(address);
        }
    }
}