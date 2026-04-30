using EEBUS;
using EEBUS.Models;
using EEBUS.Net.EEBUS.Data.DataStructures;

namespace TestProject1.IntegrationTests;

public class LppLpcConsistencyTests
{
    private const string TestLocalSki = "662728a479fa2fcf28e6d9e7855e996ab1d850a2";
    private const string TestRemoteSki = "c09ff4c4dc2916414714662366f968f4743af7b7";

    private byte[] GetSkiBytes(string ski)
        => Enumerable.Range(0, ski.Length / 2)
            .Select(x => Convert.ToByte(ski.Substring(x * 2, 2), 16))
            .ToArray();

    /// <summary>
    /// Wenn LPC und LPP gleichzeitig aktiv sind, müssen beide Characteristics
    /// dieselbe electricalConnectionId und dieselbe parameterId verwenden –
    /// sie beschreiben denselben Netzanschluss und denselben Messpunkt.
    /// </summary>
    [Fact]
    public void LpcAndLpp_Characteristics_ShareElectricalConnectionIdAndParameterId()
    {
        var devices = new Devices();
        devices.GetOrCreateLocal(GetSkiBytes(TestLocalSki), new DeviceSettings
        {
            Name = "TestBess", Id = "Test-BESS", Model = "TestModel",
            Brand = "TestBrand", Type = "Battery", Serial = "BESS001", Port = 7208,
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
                        new UseCaseSettings
                        {
                            Type = "limitationOfPowerConsumption",
                            Actor = "ControllableSystem",
                            InitLimits = new LimitSettings
                            {
                                Active = false, Limit = 5000, Duration = TimeSpan.Zero,
                                NominalMax = 5000, FailsafeLimit = 0,
                                FailsafeDurationMinimum = TimeSpan.FromSeconds(7200),
                            },
                        },
                        new UseCaseSettings
                        {
                            Type = "limitationOfPowerProduction",
                            Actor = "ControllableSystem",
                            InitLimits = new LimitSettings
                            {
                                Active = false, Limit = 5000, Duration = TimeSpan.Zero,
                                NominalMax = 5000, FailsafeLimit = 0,
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
        Connection connection = new Client(default, default, devices, remoteDevice);

        ElectricalConnectionCharacteristicDataStructure lpcCharacteristic = connection.Local
            .GetDataStructures<ElectricalConnectionCharacteristicDataStructure>()
            .First(c => c.CharacteristicType == "contractualConsumptionNominalMax");

        ElectricalConnectionCharacteristicDataStructure lppCharacteristic = connection.Local
            .GetDataStructures<ElectricalConnectionCharacteristicDataStructure>()
            .First(c => c.CharacteristicType == "contractualProductionNominalMax");

        Assert.Equal(lpcCharacteristic.ElectricalConnectionId, lppCharacteristic.ElectricalConnectionId);
        Assert.Equal(lpcCharacteristic.ParameterId, lppCharacteristic.ParameterId);
    }
}
