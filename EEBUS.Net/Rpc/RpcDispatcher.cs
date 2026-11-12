using EEBUS.Net.Rpc.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EEBUS.Net.Rpc;

/// <summary>
/// Calls the <see cref="IRpcHandler"/> method matching an RPC method name: reads and validates the parameters and converts the result to JSON.
/// Throws <see cref="RpcException"/> for unknown methods and invalid parameters; exceptions from the handler are passed on.
/// Parameter names are deliberately inconsistent (ski, remoteSki, remoteSKI; duration, durationSeconds): they must match the client exactly.
/// </summary>
internal sealed class RpcDispatcher
{
    // Wire format: camelCase properties, unset optional values omitted, enums as their exact wire names (no integers).
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private readonly IRpcHandler _handler;

    public RpcDispatcher(IRpcHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    /// <summary>Invokes <paramref name="method"/> and returns its JSON result (null for methods without a result).</summary>
    public async Task<JsonNode?> InvokeAsync(string method, JsonNode? parameters, CancellationToken ct)
    {
        JsonObject args = parameters switch
        {
            null => new JsonObject(),
            JsonObject obj => obj,
            _ => throw InvalidParamsException(method, "params must be an object")
        };

        IRpcHandler h = _handler;
        Task<JsonNode?> invocation = method switch
        {
            // Lifecycle
            "WaitForOnline" => NoResult(h.WaitForOnlineAsync(GetRequiredNumber(args, method, "timeoutSeconds"), ct)),
            "TrustDevice" => NoResult(h.TrustDeviceAsync(GetRequiredString(args, method, "ski"), ct)),
            "DeleteTrustedDeviceSKI" => NoResult(h.DeleteTrustedDeviceSkiAsync(GetRequiredString(args, method, "ski"), ct)),
            "Reboot" => NoResult(h.RebootAsync(ct)),
            "SwitchPower" => NoResult(h.SwitchPowerAsync(GetRequiredBoolean(args, method, "on"), ct)),
            "PerformFactoryReset" => NoResult(h.PerformFactoryResetAsync(ct)),

            // LPC - Controllable System
            "GetLPCState" => Result(h.GetLpcStateAsync(ct)),
            "IsHeartbeatReceived" => Result(h.IsHeartbeatReceivedAsync(ct)),
            "GetOwnActivePowerConsumptionWatt" => Result(h.GetOwnActivePowerConsumptionWattAsync(ct)),
            "GetOwnActivePowerConsumptionLimit" => PowerLimit(h.GetOwnActivePowerConsumptionLimitAsync(ct)),

            // LPC - Energy Guard
            "SetPowerConsumptionLimit" => Result(h.SetPowerConsumptionLimitAsync(
                GetRequiredString(args, method, "remoteSKI"),
                GetRequiredNumber(args, method, "valueWatts"),
                GetRequiredBoolean(args, method, "active"),
                GetOptionalNumber(args, method, "durationSeconds"),
                ct)),
            "SetFailSafeValue" => Result(h.SetFailSafeValueAsync(
                GetRequiredString(args, method, "remoteSKI"),
                GetRequiredNumber(args, method, "valueWatts"),
                ct)),
            "SetFailSafeDuration" => Result(h.SetFailSafeDurationAsync(
                GetRequiredString(args, method, "remoteSKI"),
                GetRequiredNumber(args, method, "duration"),
                ct)),

            // LPP - Controllable System
            "GetLPPState" => Result(h.GetLppStateAsync(ct)),
            "GetOwnActivePowerProductionWatt" => Result(h.GetOwnActivePowerProductionWattAsync(ct)),
            "GetOwnActivePowerProductionLimit" => PowerLimit(h.GetOwnActivePowerProductionLimitAsync(ct)),

            // LPP - Energy Guard
            "SetPowerProductionLimit" => Result(h.SetPowerProductionLimitAsync(
                GetRequiredString(args, method, "remoteSKI"),
                GetRequiredNumber(args, method, "valueWatts"),
                GetRequiredBoolean(args, method, "active"),
                GetOptionalNumber(args, method, "durationSeconds"),
                ct)),
            "SetFailSafeProductionValue" => Result(h.SetFailSafeProductionValueAsync(
                GetRequiredString(args, method, "remoteSKI"),
                GetRequiredNumber(args, method, "valueWatts"),
                ct)),
            "SetFailSafeProductionDuration" => Result(h.SetFailSafeProductionDurationAsync(
                GetRequiredString(args, method, "remoteSKI"),
                GetRequiredNumber(args, method, "duration"),
                ct)),

            // MPC
            "ApplyMeasurementChange" => Result(h.ApplyMeasurementChangeAsync(
                GetRequiredEnum<RpcMeasurand>(args, method, "measurand"),
                GetRequiredEnum<RpcPhase>(args, method, "phase"),
                GetRequiredNumber(args, method, "value"),
                GetRequiredEnum<RpcValueState>(args, method, "valueState"),
                ct)),
            "GetMpcMeasurements" => Result(h.GetMpcMeasurementsAsync(GetRequiredString(args, method, "remoteSki"), ct)),

            // OHPCF - Customer Energy Manager
            "GetOHPCFProcess" => Result(h.GetOhpcfProcessAsync(ct)),
            "ScheduleOHPCFProcess" => NoResult(h.ScheduleOhpcfProcessAsync(GetRequiredNumber(args, method, "secondsUntilStart"), ct)),
            "StopOHPCFProcess" => NoResult(h.StopOhpcfProcessAsync(ct)),
            "PauseOHPCFProcess" => NoResult(h.PauseOhpcfProcessAsync(ct)),
            "ResumeOHPCFProcess" => NoResult(h.ResumeOhpcfProcessAsync(ct)),

            // SHIP Pairing Service
            "GetQRCodeContent" => QrCodeContent(h.GetQrCodeContentAsync(ct)),

            _ => throw new RpcException(RpcException.MethodNotFound, $"Method not found: {method}")
        };

        return await invocation.ConfigureAwait(false);
    }

    private static async Task<JsonNode?> NoResult(Task task)
    {
        await task.ConfigureAwait(false);
        return null;
    }

    private static async Task<JsonNode?> Result<T>(Task<T> task)
        => JsonSerializer.SerializeToNode(await task.ConfigureAwait(false), JsonOptions);

    // A missing limit is answered with an empty object.
    private static async Task<JsonNode?> PowerLimit(Task<RpcPowerLimit?> task)
        => await Result(task).ConfigureAwait(false) ?? new JsonObject();

    private static async Task<JsonNode?> QrCodeContent(Task<string> task)
        => await task.ConfigureAwait(false) is { Length: > 0 } content
            ? JsonValue.Create(content)
            : throw new InvalidOperationException("GetQRCodeContent returned no content.");

    private static JsonNode? GetRequired(JsonObject args, string method, string name)
        => args.TryGetPropertyValue(name, out JsonNode? node) ? node : throw InvalidParamsException(method, $"missing '{name}'");

    private static string GetRequiredString(JsonObject args, string method, string name)
    {
        JsonNode? node = GetRequired(args, method, name);
        return node?.GetValueKind() == JsonValueKind.String && node.GetValue<string>() is { Length: > 0 } value
            ? value
            : throw InvalidParamsException(method, $"'{name}' must be a non-empty string");
    }

    private static double GetRequiredNumber(JsonObject args, string method, string name)
        => TryGetNumber(GetRequired(args, method, name), out double value)
            ? value
            : throw InvalidParamsException(method, $"'{name}' must be a number");

    private static double? GetOptionalNumber(JsonObject args, string method, string name)
    {
        if (!args.TryGetPropertyValue(name, out JsonNode? node) || node is null)
            return null;

        return TryGetNumber(node, out double value)
            ? value
            : throw InvalidParamsException(method, $"'{name}' must be a number or null");
    }

    private static bool GetRequiredBoolean(JsonObject args, string method, string name)
        => GetRequired(args, method, name)?.GetValueKind() switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw InvalidParamsException(method, $"'{name}' must be a boolean")
        };

    private static bool TryGetNumber(JsonNode? node, out double value)
    {
        value = 0;
        return node is JsonValue jsonValue && jsonValue.GetValueKind() == JsonValueKind.Number && jsonValue.TryGetValue(out value);
    }

    private static T GetRequiredEnum<T>(JsonObject args, string method, string name) where T : struct, Enum
    {
        JsonNode? node = GetRequired(args, method, name);
        try
        {
            return node.Deserialize<T>(JsonOptions);
        }
        catch (JsonException)
        {
            throw InvalidParamsException(method, $"invalid value for '{name}': {node?.ToJsonString() ?? "null"}");
        }
    }

    private static RpcException InvalidParamsException(string method, string detail)
        => new(RpcException.InvalidParams, $"Invalid params for {method}: {detail}");
}
