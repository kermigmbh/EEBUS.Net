using EEBUS;
using EEBUS.DataStructures;
using EEBUS.Features;
using EEBUS.MeasurementData;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Data.DataStructures;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TestProject1.IntegrationTests.Lpc_Mpc
{
    /// <summary>
    /// Integrationstests für die ID-Konsistenz zwischen LPP (Limitation of Power Production)
    /// und MPC (Monitoring of Power Consumption) auf demselben Gerät.
    ///
    /// Hintergrund: Ein einspeisefähiges Gerät – z. B. eine PV-Anlage oder ein BESS –
    /// führt LPP und MPC parallel. Die IDs in LPP müssen auf dieselben Messwerte und
    /// Verbindungsparameter zeigen, die MPC beschreibt.
    ///
    /// Geprüfte Konsistenz-Invarianten (analog zu LpcMpcConsistencyTests):
    ///   1. LPP.limit.measurementId      == MPC.acPowerTotal.measurementId
    ///   2. LPP.characteristic.electricalConnectionId == MPC.acPowerTotal.electricalConnectionId
    ///   3. LPP.characteristic.parameterId verweist auf den acPowerTotal-Parameter in MPC
    /// </summary>
    public class LppMpcConsistencyTests
    {
        private const string TestLocalSki  = "662728a479fa2fcf28e6d9e7855e996ab1d850a2";
        private const string TestRemoteSki = "c09ff4c4dc2916414714662366f968f4743af7b7";

        public LppMpcConsistencyTests()
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
        /// Gerät mit MPC (MonitoredUnit) und LPP (ControllableSystem) auf derselben Entity.
        /// Entspricht z. B. einer PV-Anlage mit Eigenmetering.
        /// </summary>
        private Connection GetLppMpcConnection()
        {
            var devices = new Devices();
            devices.GetOrCreateLocal(GetSkiBytes(TestLocalSki), new DeviceSettings
            {
                Name   = "TestPvSystem",
                Id     = "Test-PV-System",
                Model  = "TestModel",
                Brand  = "TestBrand",
                Type   = "Inverter",
                Serial = "PV001",
                Port   = 7207,
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
                                Type       = "limitationOfPowerProduction",
                                Actor      = "ControllableSystem",
                                InitLimits = new LimitSettings
                                {
                                    Active                  = false,
                                    Limit                   = 10000,
                                    Duration                = TimeSpan.Zero,
                                    NominalMax              = 10000,
                                    FailsafeLimit           = 0,
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
        /// LPP begrenzt die eingespeiste Gesamtleistung (acPowerTotal).
        /// Daher muss die measurementId im LPP-Limit-Eintrag auf denselben
        /// MPC-Messwert zeigen wie bei LPC.
        /// </summary>
        [Fact]
        public void Lpp_Limit_MeasurementId_MatchesMpc_AcPowerTotal_MeasurementId()
        {
            Connection connection = GetLppMpcConnection();

            LoadControlLimitDataStructure limit = connection.Local
                .GetDataStructures<LoadControlLimitDataStructure>()
                .First(l => l.LimitDirection == "produce");

            MeasurementData acPowerTotal = GetAcPowerTotalMeasurement(connection);

            Assert.Equal(acPowerTotal.measurementId, limit.DescriptionData.measurementId);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Konsistenz 2: electricalConnectionId
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Die electricalConnectionId im LPP-Characteristic-Eintrag muss mit der
        /// electricalConnectionId übereinstimmen, die MPC für acPowerTotal verwendet.
        /// Beide Einträge beschreiben denselben physischen Netzanschluss.
        /// </summary>
        [Fact]
        public void Lpp_Characteristic_ElectricalConnectionId_MatchesMpc_AcPowerTotal_ElectricalConnectionId()
        {
            Connection connection = GetLppMpcConnection();

            ElectricalConnectionCharacteristicDataStructure characteristic = connection.Local
                .GetDataStructures<ElectricalConnectionCharacteristicDataStructure>()
                .First(c => c.CharacteristicType == "contractualProductionNominalMax");

            MeasurementData acPowerTotal = GetAcPowerTotalMeasurement(connection);
            uint mpcElectricalConnectionId =
                acPowerTotal.electricalConnectionParameterDescriptionData!.electricalConnectionId;

            Assert.Equal(mpcElectricalConnectionId, characteristic.ElectricalConnectionId);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Konsistenz 3: parameterId → acPowerTotal
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Die parameterId im LPP-Characteristic-Eintrag muss auf den
        /// Parametereintrags-Index des acPowerTotal-Messwerts verweisen –
        /// identisch zur Anforderung bei LPC, da beide denselben Messwert referenzieren.
        /// </summary>
        [Fact]
        public void Lpp_Characteristic_ParameterId_CorrespondsTo_AcPowerTotal_ParameterDescription()
        {
            Connection connection = GetLppMpcConnection();

            ElectricalConnectionCharacteristicDataStructure characteristic = connection.Local
                .GetDataStructures<ElectricalConnectionCharacteristicDataStructure>()
                .First(c => c.CharacteristicType == "contractualProductionNominalMax");

            List<MeasurementData> measurements = GetMeasurementFeature(connection).measurementData;
            int acPowerTotalIndex = measurements
                .FindIndex(m => m.measurementDescriptionDataType?.scopeType == "acPowerTotal");

            Assert.True(acPowerTotalIndex >= 0, "acPowerTotal nicht in MPC-Messdaten gefunden.");
            Assert.Equal((uint)acPowerTotalIndex, characteristic.ParameterId);
        }
    }
}
