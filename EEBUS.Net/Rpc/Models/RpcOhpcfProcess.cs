using System.Text.Json.Serialization;

namespace EEBUS.Net.Rpc.Models;

/// <summary>Lifecycle state of an OHPCF optional power consumption process.</summary>
public enum RpcOhpcfState
{
    [JsonStringEnumMemberName("inactive")] Inactive,
    [JsonStringEnumMemberName("scheduled")] Scheduled,
    [JsonStringEnumMemberName("running")] Running,
    [JsonStringEnumMemberName("paused")] Paused,
    [JsonStringEnumMemberName("completed")] Completed,
    [JsonStringEnumMemberName("invalid")] Invalid
}

/// <summary>Whether an OHPCF power value is an approximation or a maximum.</summary>
public enum RpcPowerValueType
{
    [JsonStringEnumMemberName("power")] Power,
    [JsonStringEnumMemberName("powerMax")] PowerMax
}

/// <summary>An OHPCF optional power consumption process as observed by the CEM. Unset optional values are omitted from the response.</summary>
/// <param name="PowerValueWatt">Power magnitude of the process in watts.</param>
public sealed record RpcOhpcfProcess(RpcOhpcfState State, RpcPowerValueType PowerValueType, double PowerValueWatt)
{
    /// <summary>Scheduled start; null when not yet scheduled.</summary>
    public DateTimeOffset? ScheduledStartTime { get; init; }

    /// <summary>True when the compressor has not announced a known active duration.</summary>
    public bool? DurationUnknown { get; init; }

    /// <summary>True when the CEM may send a stop command.</summary>
    public bool? IsStoppable { get; init; }

    /// <summary>True when the CEM may send pause and resume commands.</summary>
    public bool? IsPausable { get; init; }

    /// <summary>Minimum active duration in seconds announced by the compressor.</summary>
    public double? ActiveDurationMin { get; init; }

    /// <summary>Minimum pause duration in seconds announced by the compressor.</summary>
    public double? PauseDurationMin { get; init; }
}
