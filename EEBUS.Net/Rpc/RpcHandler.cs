using EEBUS.Net.Rpc.Models;

namespace EEBUS.Net.Rpc;

/// <summary>
/// Base class for the RPC methods served by <see cref="RpcServer"/>.
/// No device supports all methods: override those of the test suites you run. Methods that are not overridden are answered
/// with error -32601. A <see cref="TimeoutException"/> is answered with error -32001; any other exception with error -32000 and
/// the exception's message.
/// Every method must answer within 30 seconds (<c>WaitForOnline</c>: its timeout plus 15 seconds).
/// </summary>
public abstract class RpcHandler
{
    // Lifecycle

    /// <summary>
    /// Handles <c>WaitForOnline</c>: blocks until the device is reachable and its EEBUS stack is serving. Must actually wait;
    /// throw <see cref="TimeoutException"/> if the device is not online within <paramref name="timeoutSeconds"/>.
    /// </summary>
    public virtual Task WaitForOnlineAsync(double timeoutSeconds, CancellationToken ct) => throw RpcException.NotSupported("WaitForOnline");

    /// <summary>
    /// Handles <c>TrustDevice</c>: adds <paramref name="ski"/> to the trust store. Must succeed if the SKI is already trusted,
    /// and should return as soon as the SKI is stored, without waiting for a connection.
    /// </summary>
    public virtual Task TrustDeviceAsync(string ski, CancellationToken ct) => throw RpcException.NotSupported("TrustDevice");

    /// <summary>
    /// Handles <c>DeleteTrustedDeviceSKI</c>: removes <paramref name="ski"/> from the trust store and drops any open connection to it.
    /// Must succeed if the SKI is not trusted.
    /// </summary>
    public virtual Task DeleteTrustedDeviceSkiAsync(string ski, CancellationToken ct) => throw RpcException.NotSupported("DeleteTrustedDeviceSKI");

    /// <summary>Handles <c>Reboot</c>. Returns as soon as the restart is accepted, without waiting for the device to come back.</summary>
    public virtual Task RebootAsync(CancellationToken ct) => throw RpcException.NotSupported("Reboot");

    /// <summary>Handles <c>SwitchPower</c>: cuts (false) or restores (true) the device's mains power.</summary>
    public virtual Task SwitchPowerAsync(bool on, CancellationToken ct) => throw RpcException.NotSupported("SwitchPower");

    /// <summary>
    /// Handles <c>PerformFactoryReset</c>: erases persisted state (trust store, failsafe values, pairing data) and restarts.
    /// Returns as soon as the reset is accepted.
    /// </summary>
    public virtual Task PerformFactoryResetAsync(CancellationToken ct) => throw RpcException.NotSupported("PerformFactoryReset");

    // LPC - Controllable System

    /// <summary>Handles <c>GetLPCState</c>. Must be cheap and return the live state, as the client polls it about once per second.</summary>
    public virtual Task<RpcLimitState> GetLpcStateAsync(CancellationToken ct) => throw RpcException.NotSupported("GetLPCState");

    /// <summary>
    /// Handles <c>IsHeartbeatReceived</c>: whether the Energy Guard's heartbeat is currently arriving.
    /// Return false rather than throwing: the client treats errors as false.
    /// </summary>
    public virtual Task<bool> IsHeartbeatReceivedAsync(CancellationToken ct) => throw RpcException.NotSupported("IsHeartbeatReceived");

    /// <summary>Handles <c>GetOwnActivePowerConsumptionWatt</c>: the device's own consumption in watts, as a magnitude (zero or positive).</summary>
    public virtual Task<double> GetOwnActivePowerConsumptionWattAsync(CancellationToken ct) => throw RpcException.NotSupported("GetOwnActivePowerConsumptionWatt");

    /// <summary>Handles <c>GetOwnActivePowerConsumptionLimit</c>: the APCL the device is subject to, or null if no limit is in force.</summary>
    public virtual Task<RpcPowerLimit?> GetOwnActivePowerConsumptionLimitAsync(CancellationToken ct) => throw RpcException.NotSupported("GetOwnActivePowerConsumptionLimit");

    // LPC - Energy Guard

    /// <summary>
    /// Handles <c>SetPowerConsumptionLimit</c>: writes an active power consumption limit to the remote device <paramref name="remoteSki"/>.
    /// Returns true if the remote device accepted the limit and false if it rejected it; throw only if the write could not be attempted.
    /// </summary>
    /// <param name="valueWatts">Limit in watts. Must be sent unchanged (no clamping or rounding), even if negative.</param>
    /// <param name="active">True to activate the limit, false to deactivate it.</param>
    /// <param name="durationSeconds">Limit duration in seconds; null means indefinite (not zero, which expires immediately).</param>
    public virtual Task<bool> SetPowerConsumptionLimitAsync(string remoteSki, double valueWatts, bool active, double? durationSeconds, CancellationToken ct)
        => throw RpcException.NotSupported("SetPowerConsumptionLimit");

    /// <summary>
    /// Handles <c>SetFailSafeValue</c>: writes the failsafe consumption limit (watts, sent unchanged) to the remote device.
    /// Returns true if accepted, false if rejected.
    /// </summary>
    public virtual Task<bool> SetFailSafeValueAsync(string remoteSki, double valueWatts, CancellationToken ct) => throw RpcException.NotSupported("SetFailSafeValue");

    /// <summary>
    /// Handles <c>SetFailSafeDuration</c>: writes the failsafe duration minimum (seconds) to the remote device.
    /// Returns true if accepted, false if rejected.
    /// </summary>
    public virtual Task<bool> SetFailSafeDurationAsync(string remoteSki, double durationSeconds, CancellationToken ct) => throw RpcException.NotSupported("SetFailSafeDuration");

    // LPP - Controllable System

    /// <summary>Handles <c>GetLPPState</c>. Must be cheap and return the live state, as the client polls it about once per second.</summary>
    public virtual Task<RpcLimitState> GetLppStateAsync(CancellationToken ct) => throw RpcException.NotSupported("GetLPPState");

    /// <summary>Handles <c>GetOwnActivePowerProductionWatt</c>: the device's own production in watts, as a magnitude (zero or positive).</summary>
    public virtual Task<double> GetOwnActivePowerProductionWattAsync(CancellationToken ct) => throw RpcException.NotSupported("GetOwnActivePowerProductionWatt");

    /// <summary>Handles <c>GetOwnActivePowerProductionLimit</c>: the APPL the device is subject to (negative), or null if no limit is in force.</summary>
    public virtual Task<RpcPowerLimit?> GetOwnActivePowerProductionLimitAsync(CancellationToken ct) => throw RpcException.NotSupported("GetOwnActivePowerProductionLimit");

    // LPP - Energy Guard

    /// <summary>
    /// Handles <c>SetPowerProductionLimit</c>: writes an active power production limit to the remote device <paramref name="remoteSki"/>.
    /// Returns true if the remote device accepted the limit and false if it rejected it; throw only if the write could not be attempted.
    /// </summary>
    /// <param name="valueWatts">Limit in watts, negative for production. Must be sent unchanged: do not negate, clamp or round.</param>
    /// <param name="active">True to activate the limit, false to deactivate it.</param>
    /// <param name="durationSeconds">Limit duration in seconds; null means indefinite (not zero, which expires immediately).</param>
    public virtual Task<bool> SetPowerProductionLimitAsync(string remoteSki, double valueWatts, bool active, double? durationSeconds, CancellationToken ct)
        => throw RpcException.NotSupported("SetPowerProductionLimit");

    /// <summary>
    /// Handles <c>SetFailSafeProductionValue</c>: writes the failsafe production limit to the remote device. Unlike the APPL it is
    /// positive; send it unchanged. Returns true if accepted, false if rejected.
    /// </summary>
    public virtual Task<bool> SetFailSafeProductionValueAsync(string remoteSki, double valueWatts, CancellationToken ct) => throw RpcException.NotSupported("SetFailSafeProductionValue");

    /// <summary>
    /// Handles <c>SetFailSafeProductionDuration</c>: writes the failsafe production duration minimum (seconds) to the remote device.
    /// Returns true if accepted, false if rejected.
    /// </summary>
    public virtual Task<bool> SetFailSafeProductionDurationAsync(string remoteSki, double durationSeconds, CancellationToken ct) => throw RpcException.NotSupported("SetFailSafeProductionDuration");

    // MPC - Monitored Unit

    /// <summary>
    /// Handles <c>ApplyMeasurementChange</c>: changes one measurand of the device. Returns true if it was set to exactly
    /// <paramref name="value"/>, false if the device cannot take a specific value (it then only has to change).
    /// The device must report the value with <paramref name="valueState"/>.
    /// </summary>
    /// <param name="value">Value in its SI unit (W, Wh, A, V, Hz).</param>
    public virtual Task<bool> ApplyMeasurementChangeAsync(RpcMeasurand measurand, RpcPhase phase, double value, RpcValueState valueState, CancellationToken ct)
        => throw RpcException.NotSupported("ApplyMeasurementChange");

    // MPC - Measurement Aggregator

    /// <summary>
    /// Handles <c>GetMpcMeasurements</c>: the measurements aggregated from the Monitored Unit <paramref name="remoteSki"/>.
    /// Answer from cached state (polled once per second), report values exactly as received and keep the last good value
    /// instead of values received with state error or out of range.
    /// </summary>
    public virtual Task<RpcMpcMeasurements> GetMpcMeasurementsAsync(string remoteSki, CancellationToken ct) => throw RpcException.NotSupported("GetMpcMeasurements");

    // OHPCF - Customer Energy Manager

    /// <summary>Handles <c>GetOHPCFProcess</c>: the process the device currently observes. Throw if no heat pump data has been received yet.</summary>
    public virtual Task<RpcOhpcfProcess> GetOhpcfProcessAsync(CancellationToken ct) => throw RpcException.NotSupported("GetOHPCFProcess");

    /// <summary>Handles <c>ScheduleOHPCFProcess</c>: schedules the announced process to start <paramref name="secondsUntilStart"/> seconds from now.</summary>
    public virtual Task ScheduleOhpcfProcessAsync(double secondsUntilStart, CancellationToken ct) => throw RpcException.NotSupported("ScheduleOHPCFProcess");

    /// <summary>Handles <c>StopOHPCFProcess</c>: sends a stop command to the compressor.</summary>
    public virtual Task StopOhpcfProcessAsync(CancellationToken ct) => throw RpcException.NotSupported("StopOHPCFProcess");

    /// <summary>Handles <c>PauseOHPCFProcess</c>: sends a pause command to the compressor.</summary>
    public virtual Task PauseOhpcfProcessAsync(CancellationToken ct) => throw RpcException.NotSupported("PauseOHPCFProcess");

    /// <summary>Handles <c>ResumeOHPCFProcess</c>: sends a resume command to the compressor.</summary>
    public virtual Task ResumeOhpcfProcessAsync(CancellationToken ct) => throw RpcException.NotSupported("ResumeOHPCFProcess");

    // SHIP Pairing Service

    /// <summary>Handles <c>GetQRCodeContent</c>: the device's SHIP pairing QR code content, verbatim (<c>SHIP;SKI:...;ENDSHIP;</c>).</summary>
    public virtual Task<string> GetQrCodeContentAsync(CancellationToken ct) => throw RpcException.NotSupported("GetQRCodeContent");
}
