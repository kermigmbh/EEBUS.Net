using System.Reflection;
using System.Runtime.CompilerServices;
using EEBUS;
using EEBUS.Models;

namespace TestProject1.Mgcp
{
    /// <summary>
    /// Unit-Tests für die spezifikationskonforme Feature-Registrierung des
    /// "Monitoring of Grid Connection Point" (MGCP) Use Cases.
    /// </summary>
    public class MgcpRegistrationTests
    {
        private const string TestLocalSki  = "662728a479fa2fcf28e6d9e7855e996ab1d850a2";
        private const string TestRemoteSki = "c09ff4c4dc2916414714662366f968f4743af7b7";

        public MgcpRegistrationTests()
        {
            foreach (string ns in new[]
            {
                "EEBUS.SHIP.Messages",
                "EEBUS.SPINE.Commands",
                "EEBUS.Entities",
                "EEBUS.UseCases.GridConnectionPoint",
                "EEBUS.UseCases.MonitoringAppliance",
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
        /// Reale Gerätekonfiguration für den GridConnectionPoint-Aktor:
        /// GridConnectionPointOfPremises-Entity mit MGCP-UseCase.
        /// Die Entity selbst fügt bereits DeviceConfiguration-server,
        /// Measurement-server und ElectricalConnection-server hinzu.
        /// </summary>
        private Connection GetMgcpGridConnectionPointConnection()
        {
            var devices = new Devices();
            devices.GetOrCreateLocal(GetSkiBytes(TestLocalSki), new DeviceSettings
            {
                Name   = "TestGridMeter",
                Id     = "Test-Grid-Meter",
                Model  = "TestModel",
                Brand  = "TestBrand",
                Type   = "SubMeterElectricity",
                Serial = "GCP001",
                Port   = 7204,
                Entities =
                [
                    new EntitySettings { Type = "DeviceInformation" },
                    new EntitySettings
                    {
                        Type     = "GridConnectionPointOfPremises",
                        UseCases =
                        [
                            new UseCaseSettings
                            {
                                Type  = "monitoringOfGridConnectionPoint",
                                Actor = "GridConnectionPoint",
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
        
        private static string GetFunctionName(Function f) => f.SupportedFunction.function;

        // ══════════════════════════════════════════════════════════════════════
        // GridConnectionPoint – Pflicht-Features laut MGCP-Spec
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// EEBUS-SPINE-UC-MGCP: GridConnectionPoint MUSS Measurement-server bereitstellen.
        /// Wird sowohl von der Entity als auch vom UseCase registriert.
        /// </summary>
        [Fact]
        public void MgcpGridConnectionPoint_RegistersMeasurementServerFeature()
        {
            Connection connection = GetMgcpGridConnectionPointConnection();
            Assert.NotNull(connection.Local.GetFeatureAddress("Measurement", server: true));
            
            var feature = connection.Local.Entities
                .SelectMany(e => e.Features)
                .FirstOrDefault(f => f.Type == "Measurement" && f.Role == "server");
            Assert.NotNull(feature);
            
            Assert.Contains(feature.Functions, f => GetFunctionName(f) == "measurementDescriptionListData");
            Assert.Contains(feature.Functions, f => GetFunctionName(f) == "measurementListData");
        }

        /// <summary>
        /// EEBUS-SPINE-UC-MGCP: GridConnectionPoint MUSS ElectricalConnection-server
        /// bereitstellen, um Verbindungsparameter (electricalConnectionParameterDescriptionListData,
        /// electricalConnectionDescriptionListData) zu exponieren.
        /// Wird von Use Case und Entity registriert.
        /// </summary>
        [Fact]
        public void MgcpGridConnectionPoint_RegistersElectricalConnectionServerFeature()
        {
            Connection connection = GetMgcpGridConnectionPointConnection();
            Assert.NotNull(connection.Local.GetFeatureAddress("ElectricalConnection", server: true));
            
            var feature = connection.Local.Entities
                .SelectMany(e => e.Features)
                .FirstOrDefault(f => f.Type == "ElectricalConnection" && f.Role == "server");
            Assert.NotNull(feature);
            
            Assert.Contains(feature.Functions, f => GetFunctionName(f) == "electricalConnectionDescriptionListData");
            Assert.Contains(feature.Functions, f => GetFunctionName(f) == "electricalConnectionParameterDescriptionListData");
        }

        /// <summary>
        /// EEBUS-SPINE-UC-MGCP: GridConnectionPoint MUSS DeviceConfiguration-server
        /// bereitstellen (z. B. für PvCurtailmentLimitFactor, Szenario 1).
        /// Wird von der GridConnectionPointOfPremises-Entity registriert.
        /// </summary>
        [Fact]
        public void MgcpGridConnectionPoint_RegistersDeviceConfigurationServerFeature()
        {
            Connection connection = GetMgcpGridConnectionPointConnection();
            Assert.NotNull(connection.Local.GetFeatureAddress("DeviceConfiguration", server: true));
            
            var feature = connection.Local.Entities
                .SelectMany(e => e.Features)
                .FirstOrDefault(f => f.Type == "DeviceConfiguration" && f.Role == "server");
            Assert.NotNull(feature);
            
            Assert.Contains(feature.Functions, f => GetFunctionName(f) == "deviceConfigurationKeyValueDescriptionListData");
            Assert.Contains(feature.Functions, f => GetFunctionName(f) == "deviceConfigurationKeyValueListData");
        }
    }
}
