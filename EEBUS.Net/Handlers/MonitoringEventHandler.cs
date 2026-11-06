using EEBUS.Models;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.Net.Extensions;
using EEBUS.UseCases.ControllableSystem;

namespace EEBUS.Net.Handlers;

/// <summary>
/// Forwards measurement and configuration updates of remote devices as <see cref="DeviceData"/>.
/// </summary>
internal sealed class MonitoringEventHandler(EEBUSManager manager) : MonitoringUseCaseEvents
{
    public Task DataUpdateMeasurementsAsync(List<MeasurementData.MeasurementData> measurementData, string ski)
        => manager.RaiseDeviceDataChangedAsync(new DeviceData { SKI = ski, Measurements = measurementData.CollectData() });

    public Task DeviceConfigurationChangedAsync(Device device)
        => manager.RaiseDeviceDataChangedAsync(new DeviceData { SKI = device.SKI.ToString(), Mgcp = device.ReadMgcpData() });
}
