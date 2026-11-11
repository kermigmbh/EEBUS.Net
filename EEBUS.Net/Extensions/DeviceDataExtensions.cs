using EEBUS.DataStructures;
using EEBUS.Features;
using EEBUS.KeyValues;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net.EEBUS;
using EEBUS.Net.EEBUS.Data.DataStructures;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.StateMachines;
using System.Xml;

namespace EEBUS.Net.Extensions;

/// <summary>
/// Maps the SPINE device model to the public <see cref="DeviceData"/> and reads/writes it over a connection.
/// </summary>
internal static class DeviceDataExtensions
{
    extension(LocalDevice local)
    {
        public DeviceData ToDeviceData(EffectiveLimit lpcEffectiveLimit, EffectiveLimit lppEffectiveLimit)
        {
            LpcLppData lpc = new() { LimitActive = false, Limit = 0, LimitDuration = 0, FailSafeLimit = 0, ContractualNominalMax = 0 };
            LpcLppData lpp = new() { LimitActive = false, Limit = 0, LimitDuration = 0, FailSafeLimit = 0, ContractualNominalMax = 0 };

            foreach (LoadControlLimitDataStructure data in local.GetDataStructures<LoadControlLimitDataStructure>())
            {
                if (data.LimitDirection == "consume")
                    lpc.ApplyLimit(data, lpcEffectiveLimit);
                else if (data.LimitDirection == "produce")
                    lpp.ApplyLimit(data, lppEffectiveLimit);
            }

            foreach (ElectricalConnectionCharacteristicDataStructure data in local.GetDataStructures<ElectricalConnectionCharacteristicDataStructure>())
            {
                if (data.CharacteristicType == "contractualConsumptionNominalMax")
                    lpc.ContractualNominalMax = ScaledNumber.ToLong(data.Number, data.Scale);
                else if (data.CharacteristicType == "contractualProductionNominalMax")
                    lpp.ContractualNominalMax = ScaledNumber.ToLong(data.Number, data.Scale);
            }

            if (local.GetKeyValue<FailsafeConsumptionActivePowerLimitKeyValue>() is { } lpcFailsafe)
                lpc.FailSafeLimit = ScaledNumber.ToLong(lpcFailsafe.Value, lpcFailsafe.Scale);

            if (local.GetKeyValue<FailsafeProductionActivePowerLimitKeyValue>() is { } lppFailsafe)
                lpp.FailSafeLimit = ScaledNumber.ToLong(lppFailsafe.Value, lppFailsafe.Scale);

            TimeSpan failsafeDuration = local.GetKeyValue<FailsafeDurationMinimumKeyValue>() is { } failsafeDurationKeyValue
                ? XmlConvert.ToTimeSpan(failsafeDurationKeyValue.Duration)
                : TimeSpan.Zero;

            return new DeviceData
            {
                SKI = local.SKI.ToString(),
                Lpc = lpc,
                Lpp = lpp,
                Measurements = local.ReadMeasurements(),
                FailSafe = new FailSafeData { LimitDuration = (int)failsafeDuration.TotalSeconds },
                UseCaseSupport = local.GetUseCaseSupport(),
                ShipId = local.ShipID
            };
        }
    }

    extension(RemoteDevice remote)
    {
        public DeviceData ToDeviceData() => new()
        {
            SKI = remote.SKI.ToString(),
            Measurements = remote.ReadMeasurements(),
            Mgcp = remote.ReadMgcpData() ?? new MgcpData(),
            UseCaseSupport = remote.GetUseCaseSupport(),
            Name = remote.Name,
        };
    }

    extension(Device device)
    {
        /// <summary>
        /// Returns the MGCP data of the device, or null if it does not provide the PV curtailment limit factor.
        /// </summary>
        public MgcpData? ReadMgcpData()
        {
            KeyValue? factor = device.KeyValues.FirstOrDefault(kv => kv.KeyName == "pvCurtailmentLimitFactor");
            return factor == null ? null : new MgcpData { PvCurtailmentLimitFactor = factor.Data.value.scaledNumber?.ToLong() };
        }

        public MeasurementsData? ReadMeasurements()
        {
            AddressType? address = device.GetFeatureAddress("Measurement", true);
            if (address == null)
                return null;

            Entity? entity = device.Entities.FirstOrDefault(e => e.Index.SequenceEqual(address.entity));
            MeasurementServerFeature? feature = entity?.Features.FirstOrDefault(f => f.Index == address.feature) as MeasurementServerFeature;
            return feature?.measurementData.CollectData();
        }
    }

    extension(Connection connection)
    {
        /// <summary>
        /// Writes all parts of <paramref name="deviceData"/> that are set to the remote device.
        /// </summary>
        public async Task WriteAsync(DeviceData deviceData)
        {
            foreach (SpineCmdPayloadBase.Class payload in GetPayloads(deviceData))
                await payload.WriteDataAsync(connection, deviceData).ConfigureAwait(false);
        }

        /// <summary>
        /// Fills all parts of <paramref name="deviceData"/> that are set with data read from the remote device.
        /// </summary>
        public async Task ReadAsync(DeviceData deviceData)
        {
            foreach (SpineCmdPayloadBase.Class payload in GetPayloads(deviceData))
                await payload.ReadDataAsync(connection, deviceData).ConfigureAwait(false);
        }
    }

    private static void ApplyLimit(this LpcLppData target, LoadControlLimitDataStructure data, EffectiveLimit effectiveLimit)
    {
        TimeSpan duration = data.EndTime == null ? Timeout.InfiniteTimeSpan : XmlConvert.ToTimeSpan(data.EndTime);
        target.LimitActive = effectiveLimit.IsLimited;
        //use the effective limit, not the limit from the data structure (in failsafe mode the failsafe limit is reported)
        target.Limit = effectiveLimit.Value;
        target.LimitDuration = (int)duration.TotalSeconds;
    }

    private static IEnumerable<SpineCmdPayloadBase.Class> GetPayloads(DeviceData deviceData)
    {
        List<string> cmds = [];
        if (deviceData.Lpc != null || deviceData.Lpp != null)
            cmds.AddRange(["loadControlLimitListData", "electricalConnectionCharacteristicListData"]);
        cmds.Add("deviceConfigurationKeyValueListData");
        if (deviceData.Measurements != null)
            cmds.Add("measurementListData");

        return cmds.Select(SpineCmdPayloadBase.GetClass).OfType<SpineCmdPayloadBase.Class>();
    }
}
