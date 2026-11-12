namespace EEBUS.Net.Rpc.Models;

/// <summary>A request that is answered with the given JSON-RPC error code, rather than as an internal error.</summary>
internal sealed class RpcException(int code, string message) : Exception(message)
{
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;

    public int Code { get; } = code;

    /// <summary>For methods the <see cref="RpcHandler"/> does not override.</summary>
    public static RpcException NotSupported(string method) => new(MethodNotFound, $"Method not supported: {method}");
}
