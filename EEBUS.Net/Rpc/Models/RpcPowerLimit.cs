namespace EEBUS.Net.Rpc.Models;

/// <summary>
/// Active power consumption limit (APCL) or active power production limit (APPL) a device is subject to.
/// Unset optional values are omitted from the response.
/// </summary>
/// <param name="Active">Whether the limit is currently active.</param>
/// <param name="Value">Limit value in watts. An APPL is negative, an APCL is positive.</param>
public sealed record RpcPowerLimit(bool Active, double Value)
{
    /// <summary>Limit duration in seconds; null if indefinite.</summary>
    public double? DurationSeconds { get; init; }

    /// <summary>True when the duration is indefinite.</summary>
    public bool? DurationIndefinite { get; init; }

    /// <summary>Whether the Energy Guard can change the limit.</summary>
    public bool? IsLimitChangeable { get; init; }
}
