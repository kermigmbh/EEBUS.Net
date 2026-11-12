using System.Text.Json.Serialization;

namespace EEBUS.Net.Rpc.Models;

/// <summary>Measured quantity for <c>ApplyMeasurementChange</c>.</summary>
public enum RpcMeasurand
{
    [JsonStringEnumMemberName("power-total")] PowerTotal,
    [JsonStringEnumMemberName("power-phase")] PowerPhase,
    [JsonStringEnumMemberName("energy-consumed")] EnergyConsumed,
    [JsonStringEnumMemberName("energy-produced")] EnergyProduced,
    [JsonStringEnumMemberName("current")] Current,
    [JsonStringEnumMemberName("voltage")] Voltage,
    [JsonStringEnumMemberName("frequency")] Frequency
}

/// <summary>Phase of a measurement: line-to-neutral (a, b, c), line-to-line (ab, bc, ac) or none for non-phase measurands.</summary>
public enum RpcPhase
{
    [JsonStringEnumMemberName("")] None,
    [JsonStringEnumMemberName("a")] A,
    [JsonStringEnumMemberName("b")] B,
    [JsonStringEnumMemberName("c")] C,
    [JsonStringEnumMemberName("ab")] AB,
    [JsonStringEnumMemberName("bc")] BC,
    [JsonStringEnumMemberName("ac")] AC
}

/// <summary>Quality flag of a measurement.</summary>
public enum RpcValueState
{
    [JsonStringEnumMemberName("normal")] Normal,
    [JsonStringEnumMemberName("error")] Error,
    [JsonStringEnumMemberName("outOfRange")] OutOfRange
}

/// <summary>A measured value in its SI unit (W, Wh, A, V, Hz). Unset optional values are omitted from the response.</summary>
/// <param name="Value">The value exactly as received: unrounded and unscaled. Consumption is positive, production negative.</param>
public sealed record RpcMeasurement(double Value)
{
    public RpcValueState? ValueState { get; init; }

    public DateTimeOffset? Timestamp { get; init; }
}

/// <summary>
/// Measurements aggregated from a Monitored Unit (<c>GetMpcMeasurements</c>). Leave channels that are not aggregated null:
/// omitted channels are not checked, while a channel with a placeholder value fails.
/// </summary>
public sealed record RpcMpcMeasurements
{
    public RpcMeasurement? PowerTotal { get; init; }

    public IReadOnlyDictionary<RpcPhase, RpcMeasurement>? PowerPerPhase { get; init; }

    public RpcMeasurement? EnergyConsumption { get; init; }

    public RpcMeasurement? EnergyProduction { get; init; }

    public IReadOnlyDictionary<RpcPhase, RpcMeasurement>? CurrentPerPhase { get; init; }

    public IReadOnlyDictionary<RpcPhase, RpcMeasurement>? VoltagePerPhase { get; init; }

    public RpcMeasurement? Frequency { get; init; }
}
