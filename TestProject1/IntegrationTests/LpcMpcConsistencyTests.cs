using System.Reflection;
using System.Runtime.CompilerServices;
using EEBUS;
using EEBUS.DataStructures;
using EEBUS.Features;
using EEBUS.MeasurementData;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Data.DataStructures;

namespace TestProject1.IntegrationTests
{
    /// <summary>
    /// Integrationstests für die ID-Konsistenz zwischen LPC (Limitation of Power Consumption)
    /// und MPC (Monitoring of Power Consumption) auf demselben Gerät.
    ///
    /// Hintergrund: Ein Gerät, das sowohl kontrollierbar (LPC) als auch messend (MPC) ist –
    /// z. B. eine Wärmepumpe – führt beide Use Cases parallel. Die IDs in LPC müssen auf
    /// dieselben Messwerte und Verbindungsparameter zeigen, die MPC beschreibt.
    ///
    /// Geprüfte Konsistenz-Invarianten:
    ///   1. LPC.limit.measurementId      == MPC.acPowerTotal.measurementId
    ///   2. LPC.characteristic.electricalConnectionId == MPC.acPowerTotal.electricalConnectionId
    ///   3. LPC.characteristic.parameterId verweist auf den acPowerTotal-Parameter in MPC
    /// </summary>
    public class LpcMpcConsistencyTests
    {
        private const string TestLocalSki  = "662728a479fa2fcf28e6d9e7855e996ab1d850a2";
        private const string TestRemoteSki = "c09ff4c4dc2916414714662366f968f4743af7b7";

        public LpcMpcConsistencyTests()
        {
            foreach (string ns in new[]
            {
                "EEBUS.SHIP.Messages",
                "EEBUS.SPINE.Commands",
                "EEBUS.Entities",
                "EEBUS.UseCases.MonitoredUnit",
                "EEBUS.UseCases.ControllableSystem",
                "EEBUS.Features",
            })
            {
                foreach (Type type in GetTypesInNamespace(typeof(Settings).Assembly, ns))
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Hilfsmethoden
        // ──────────────────────────────────────────────────────────────────────

        private static Type[] GetTypesInNamespace(Assembly assembly, string nameSpace)
            => assembly.GetTypes()
                       .Where(t => string.Equals(t.Namespace, nameSpace, StringComparison.Ordinal))
                       .ToArray();

        private byte[] GetSkiBytes(string ski)
            => Enumerable.Range(0, ski.Length / 2)
                         .Select(x => Convert.ToByte(ski.Substring(x * 2, 2), 16))
                         .ToArray();

        /// <summary>
        /// Gerät mit MPC (MonitoredUnit) und LPC (ControllableSystem) auf derselben Entity.
        /// Entspricht einer Wärmepumpe oder ähnlichem steuerbaren Verbraucher mit Eigenmetering.
        /// </summary>
        private Connection GetLpcMpcConnection()
        {
            var devices = new Devices();
            devices.GetOrCreateLocal(GetSkiBytes(TestLocalSki), new DeviceSettings
            {
                Name   = "TestHeatPump",
                Id     = "Test-Heat-Pump",
                Model  = "TestModel",
                Brand  = "TestBrand",
                Type   = "HeatPump",
                Serial = "HP001",
                Port   = 7206,
                Entities =
                [
                    new EntitySettings { Type = "DeviceInformation" },
                    new EntitySettings
                    {
                        Type     = "CEM",
                        UseCases =
                        [
                            new UseCaseSettings
                            {
                                Type  = "monitoringOfPowerConsumption",
                                Actor = "MonitoredUnit",
                            },
                            new UseCaseSettings
                            {
                                Type       = "limitationOfPowerConsumption",
                                Actor      = "ControllableSystem",
                                InitLimits = new LimitSettings
                                {
                                    Active                = false,
                                    Limit                 = 40000,
                                    Duration              = TimeSpan.Zero,
                                    NominalMax            = 40000,
                                    FailsafeLimit         = 4200,
                                    FailsafeDurationMinimum = TimeSpan.FromSeconds(7200),
                                },
                            },
                        ],
                    },
                ],
            });
            var remoteDevice = new RemoteDevice(
                "TestRemote", TestRemoteSki, string.Empty, "TestRemote", default, default);
            devices.Remote.Add(remoteDevice);
            return new Client(default, default, devices, remoteDevice);
        }

        private static MeasurementServerFeature GetMeasurementFeature(Connection connection)
            => connection.Local.Entities
                .SelectMany(e => e.Features)
                .OfType<MeasurementServerFeature>()
                .First();

        /// <summary>
        /// Gibt den MPC-Messdateneintrag für scopeType=acPowerTotal zurück.
        /// Dynamisch: sucht nach semantischem Typ, nicht nach hartkodiertem Index.
        /// </summary>
        private static MeasurementData GetAcPowerTotalMeasurement(Connection connection)
        {
            MeasurementData? entry = GetMeasurementFeature(connection).measurementData
                .FirstOrDefault(m => m.measurementDescriptionDataType?.scopeType == "acPowerTotal");

            Assert.NotNull(entry);
            return entry;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Konsistenz 1: measurementId
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// LPC steuert die Gesamtwirkleistung (acPowerTotal).
        /// Daher muss die measurementId im LPC-Limit-Eintrag auf genau diesen
        /// MPC-Messwert zeigen.
        ///
        /// Dynamisch: acPowerTotal wird per scopeType gesucht – kein hartkodierter Index.
        /// Fängt Regressionen ab, wenn MPC die Messreihenfolge ändert.
        /// </summary>
        [Fact]
        public void Lpc_Limit_MeasurementId_MatchesMpc_AcPowerTotal_MeasurementId()
        {
            Connection connection = GetLpcMpcConnection();

            LoadControlLimitDataStructure limit = connection.Local
                .GetDataStructures<LoadControlLimitDataStructure>()
                .First(l => l.LimitDirection == "consume");

            MeasurementData acPowerTotal = GetAcPowerTotalMeasurement(connection);

            Assert.Equal(acPowerTotal.measurementId, limit.DescriptionData.measurementId);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Konsistenz 2: electricalConnectionId
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Die electricalConnectionId im LPC-Characteristic-Eintrag muss mit der
        /// electricalConnectionId übereinstimmen, die MPC für den acPowerTotal-Messwert
        /// verwendet.
        ///
        /// Hintergrund: Beide Einträge beschreiben denselben physischen Netzanschluss.
        /// </summary>
        [Fact]
        public void Lpc_Characteristic_ElectricalConnectionId_MatchesMpc_AcPowerTotal_ElectricalConnectionId()
        {
            Connection connection = GetLpcMpcConnection();

            ElectricalConnectionCharacteristicDataStructure characteristic = connection.Local
                .GetDataStructures<ElectricalConnectionCharacteristicDataStructure>()
                .First(c => c.CharacteristicType == "contractualConsumptionNominalMax");

            MeasurementData acPowerTotal = GetAcPowerTotalMeasurement(connection);
            uint mpcElectricalConnectionId =
                acPowerTotal.electricalConnectionParameterDescriptionData!.electricalConnectionId;

            Assert.Equal(mpcElectricalConnectionId, characteristic.ElectricalConnectionId);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Konsistenz 3: parameterId → acPowerTotal
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Die parameterId im LPC-Characteristic-Eintrag muss auf den
        /// Parametereintrags-Index des acPowerTotal-Messwerts verweisen.
        ///
        /// Dynamisch: Der erwartete Index wird aus MPC abgeleitet – gesucht wird der
        /// Eintrag mit scopeType=acPowerTotal in der measurementData-Liste.
        /// In der aktuellen Implementierung ist das Index 0; der Test fängt Regressionen
        /// ab, wenn sich die Reihenfolge der Parameter-Beschreibungen ändert.
        ///
        /// Langfristig: Sobald ElectricalConnectionParameterDescriptionDataType.parameterId
        /// implementiert ist, sollte dieser Test auf das explizite parameterId-Feld umgestellt
        /// werden (statt auf den Listenindex zu zeigen).
        /// </summary>
        [Fact]
        public void Lpc_Characteristic_ParameterId_CorrespondsTo_AcPowerTotal_ParameterDescription()
        {
            Connection connection = GetLpcMpcConnection();

            ElectricalConnectionCharacteristicDataStructure characteristic = connection.Local
                .GetDataStructures<ElectricalConnectionCharacteristicDataStructure>()
                .First(c => c.CharacteristicType == "contractualConsumptionNominalMax");

            // Erwartete parameterId: Index des acPowerTotal-Eintrags in der
            // measurementData-Liste (= Position seiner Parameterbeschreibung).
            List<MeasurementData> measurements = GetMeasurementFeature(connection).measurementData;
            int acPowerTotalIndex = measurements
                .FindIndex(m => m.measurementDescriptionDataType?.scopeType == "acPowerTotal");

            Assert.True(acPowerTotalIndex >= 0, "acPowerTotal nicht in MPC-Messdaten gefunden.");
            Assert.Equal((uint)acPowerTotalIndex, characteristic.ParameterId);
        }
    }
}
