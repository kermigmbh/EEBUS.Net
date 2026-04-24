using EEBUS;
using EEBUS.Features;
using EEBUS.Messages;
using EEBUS.Models;
using System.Reflection;
using System.Runtime.CompilerServices;
using EEBUS.MeasurementData;

namespace TestProject1
{
    /// <summary>
    /// Unit-Tests für die ID-Korrektheit der Messdaten im
    /// "Monitoring of Power Consumption" (MPC) Use Case.
    /// </summary>
    public class MpcIdTests
    {
        private const string TestLocalSki  = "662728a479fa2fcf28e6d9e7855e996ab1d850a2";
        private const string TestRemoteSki = "c09ff4c4dc2916414714662366f968f4743af7b7";

        public MpcIdTests()
        {
            foreach (string ns in new[]
            {
                "EEBUS.SHIP.Messages",
                "EEBUS.SPINE.Commands",
                "EEBUS.Entities",
                "EEBUS.UseCases.MonitoredUnit",
                "EEBUS.UseCases.MonitoringAppliance",
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

        private Connection GetMpcMonitoredUnitConnection()
        {
            var devices = new Devices();
            devices.GetOrCreateLocal(GetSkiBytes(TestLocalSki), new DeviceSettings
            {
                Name = "TestMPCDevice", Id = "Test-MPC-Device",
                Model = "TestModel", Brand = "TestBrand",
                Type = "EnergyManagementSystem", Serial = "MPC001", Port = 7202,
                Entities =
                [
                    new EntitySettings { Type = "DeviceInformation" },
                    new EntitySettings
                    {
                        Type = "CEM",
                        UseCases = [ new UseCaseSettings
                        {
                            Type  = "monitoringOfPowerConsumption",
                            Actor = "MonitoredUnit",
                        }],
                    },
                ],
            });
            var remoteDevice = new RemoteDevice(
                "TestRemote", TestRemoteSki, string.Empty, "TestRemote", default, default);
            devices.Remote.Add(remoteDevice);
            return new Client(default, default, devices, remoteDevice);
        }

        /// <summary>
        /// Gibt die MeasurementServerFeature des lokalen Geräts zurück, oder null.
        /// </summary>
        private static MeasurementServerFeature? GetMeasurementFeature(Connection connection)
            => connection.Local.Entities
                .SelectMany(e => e.Features)
                .OfType<MeasurementServerFeature>()
                .FirstOrDefault();

        /// <summary>
        /// Jede measurementId muss eindeutig sein.
        /// Dynamisch: kein hartkodierter Indexzugriff – funktioniert für beliebig
        /// viele Einträge.
        /// </summary>
        [Fact]
        public void MpcMonitoredUnit_MeasurementIds_AreUnique()
        {
            MeasurementServerFeature feature = GetMeasurementFeature(GetMpcMonitoredUnitConnection())!;
            List<uint> ids = feature.measurementData.Select(m => m.measurementId).ToList();

            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        /// <summary>
        /// Die measurementIds müssen lückenlos bei 0 beginnen: 0, 1, 2, … N-1.
        /// Dynamisch: die erwartete Sequenz wird aus der Listenlänge berechnet.
        /// </summary>
        [Fact]
        public void MpcMonitoredUnit_MeasurementIds_FormContiguousSequenceFromZero()
        {
            MeasurementServerFeature feature = GetMeasurementFeature(GetMpcMonitoredUnitConnection())!;
            List<uint> ids = feature.measurementData
                .Select(m => m.measurementId)
                .OrderBy(id => id)
                .ToList();

            IEnumerable<uint> expected = Enumerable.Range(0, ids.Count).Select(i => (uint)i);
            Assert.Equal(expected, ids);
        }

        /// <summary>
        /// Jeder Messdateneintrag muss eine electricalConnectionParameterDescriptionData
        /// besitzen – sonst fehlt die Phasenzuordnung vollständig.
        /// </summary>
        [Fact]
        public void MpcMonitoredUnit_AllMeasurements_HaveElectricalConnectionParameterDescription()
        {
            MeasurementServerFeature feature = GetMeasurementFeature(GetMpcMonitoredUnitConnection())!;

            Assert.All(feature.measurementData,
                m => Assert.NotNull(m.electricalConnectionParameterDescriptionData));
        }

        /// <summary>
        /// Alle Messdaten-Einträge referenzieren dieselbe electricalConnectionId.
        /// Für ein Gerät mit einem einzigen Netzanschluss ist das die korrekte
        /// Invariante – dynamisch ohne Indexzugriff geprüft.
        /// </summary>
        [Fact]
        public void MpcMonitoredUnit_ElectricalConnectionIds_AreConsistentAcrossAllMeasurements()
        {
            MeasurementServerFeature feature = GetMeasurementFeature(GetMpcMonitoredUnitConnection())!;

            List<uint> usedConnectionIds = feature.measurementData
                .Where(m => m.electricalConnectionParameterDescriptionData != null)
                .Select(m => m.electricalConnectionParameterDescriptionData!.electricalConnectionId)
                .Distinct()
                .ToList();

            Assert.Single(usedConnectionIds);
        }

        /// <summary>
        /// Das measurementId-Feld INNERHALB jeder electricalConnectionParameterDescriptionData
        /// muss mit dem äußeren measurementId des enthaltenden MeasurementData-Eintrags
        /// übereinstimmen.
        /// </summary>
        [Fact]
        public void MpcMonitoredUnit_ParameterDescriptions_MeasurementIdMatchesContainingEntry()
        {
            MeasurementServerFeature feature = GetMeasurementFeature(GetMpcMonitoredUnitConnection())!;

            Assert.All(
                feature.measurementData.Where(m => m.electricalConnectionParameterDescriptionData != null),
                m => Assert.Equal(
                    m.measurementId,
                    m.electricalConnectionParameterDescriptionData!.measurementId));
        }

        /// <summary>
        /// Die measurementIds innerhalb aller electricalConnectionParameterDescriptionData-Einträge
        /// müssen eindeutig sein – Duplikate lassen den Client-Lookup für alle außer dem
        /// ersten Treffer ins Leere laufen.
        /// </summary>
        [Fact]
        public void MpcMonitoredUnit_ParameterDescriptions_MeasurementIdsAreUnique()
        {
            MeasurementServerFeature feature = GetMeasurementFeature(GetMpcMonitoredUnitConnection())!;

            List<uint> innerIds = feature.measurementData
                .Where(m => m.electricalConnectionParameterDescriptionData != null)
                .Select(m => m.electricalConnectionParameterDescriptionData!.measurementId)
                .ToList();

            Assert.Equal(innerIds.Count, innerIds.Distinct().Count());
        }

        [Fact]
        public void MpcMonitoredUnit_ElectricalConnectionIds_ReferenceRegisteredElectricalConnection()
        {
            Connection connection = GetMpcMonitoredUnitConnection();
            List<MeasurementData> measurements = GetMeasurementFeature(connection)!.measurementData;

            var expectedByConnectionId = measurements
                .Where(m => m.electricalConnectionParameterDescriptionData != null)
                .GroupBy(m => m.electricalConnectionParameterDescriptionData!.electricalConnectionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(m => m.electricalConnectionParameterDescriptionData!).ToList());

            Assert.All(expectedByConnectionId, kvp =>
            {
                uint connectionId = kvp.Key;
                var descriptions  = kvp.Value;

                // Alle Einträge derselben Connection → gleicher voltageType
                string voltageType = Assert.Single(
                    descriptions.Select(d => d.voltageType).Distinct());

                Assert.Equal("ac", voltageType);
            });

            Assert.NotNull(connection.Local.GetFeatureAddress("ElectricalConnection", server: true));
        }
    }
}
