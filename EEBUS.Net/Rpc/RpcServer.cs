using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EEBUS.Net.Rpc;

/// <summary>
/// TCP server speaking newline-delimited JSON-RPC 2.0 (one UTF-8 JSON object per line in both directions).
/// Clients are served concurrently; each connection may send any number of requests.
/// The server listens on all interfaces and can be started, stopped and restarted any number of times until it is disposed.
/// Calls to <see cref="StartAsync"/> and <see cref="StopAsync"/> must not overlap.
/// The protocol is unencrypted and unauthenticated; expose it only on trusted networks.
/// </summary>
public sealed class RpcServer : IAsyncDisposable
{
    private const int MethodNotFound = -32601;
    private const int InvalidRequest = -32600;
    private const int ParseError = -32700;

    // No byte order mark: a leading EF BB BF would make the client's JSON parser reject the first response.
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly ILogger? _logger;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private bool _disposed;

    public RpcServer(ILogger? logger = null)
    {
        _logger = logger;
    }

    public bool IsRunning => _listener != null;

    /// <summary>The port the server is bound to (the actual port when started with port 0), or null if not running.</summary>
    public int? Port => _listener == null ? null : ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>
    /// Starts accepting clients on <paramref name="port"/>. A running server is stopped first (dropping its open client connections).
    /// If the port cannot be bound, the server is not running afterwards.
    /// </summary>
    /// <exception cref="SocketException">Thrown if the port cannot be bound.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the server has been disposed.</exception>
    public async Task StartAsync(int port)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await StopAsync().ConfigureAwait(false);

        TcpListener listener = new(IPAddress.Any, port);
        try
        {
            listener.Start();
        }
        catch
        {
            // TcpListener does not release its socket when Bind fails.
            listener.Dispose();
            throw;
        }

        _listener = listener;
        _cts = new CancellationTokenSource();
        _acceptLoop = AcceptLoopAsync(listener, _cts.Token);
        _logger?.LogInformation("RPC server listening on port {Port}.", Port);
    }

    /// <summary>
    /// Stops accepting clients and cancels open client connections. Does not wait for in-flight requests to finish.
    /// Does nothing if the server is not running.
    /// </summary>
    public async Task StopAsync()
    {
        if (_listener == null || _cts == null || _acceptLoop == null)
            return;

        TcpListener listener = _listener;
        CancellationTokenSource cts = _cts;
        Task acceptLoop = _acceptLoop;
        _listener = null;
        _cts = null;
        _acceptLoop = null;

        cts.Cancel();
        listener.Stop();
        await acceptLoop.ConfigureAwait(false);
        cts.Dispose();
        _logger?.LogInformation("RPC server stopped.");
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return new ValueTask(StopAsync());
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ct.IsCancellationRequested || ex is ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                _logger?.LogWarning(ex, "RPC server failed to accept a client.");
                continue;
            }

            // Each handler disposes its client and logs its own errors.
            _ = Task.Run(() => HandleClientAsync(client, ct));
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        EndPoint? remote = client.Client.RemoteEndPoint;
        try
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();
                using StreamReader reader = new(stream, Utf8NoBom);
                using StreamWriter writer = new(stream, Utf8NoBom) { NewLine = "\n", AutoFlush = true };

                while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    _logger?.LogDebug("RPC >> {Remote}: {Request}", remote, line);
                    string response = await ProcessRequestAsync(line, ct).ConfigureAwait(false);
                    _logger?.LogDebug("RPC << {Remote}: {Response}", remote, response);

                    await writer.WriteLineAsync(response.AsMemory(), ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (IOException ex)
        {
            _logger?.LogDebug(ex, "RPC client {Remote} disconnected.", remote);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error while serving RPC client {Remote}.", remote);
        }
    }

    private Task<string> ProcessRequestAsync(string line, CancellationToken ct)
    {
        JsonNode? request;
        try
        {
            request = JsonNode.Parse(line);
        }
        catch (JsonException)
        {
            return Task.FromResult(CreateErrorResponse(null, ParseError, "Parse error"));
        }

        if (request is not JsonObject obj || obj["method"]?.GetValueKind() != JsonValueKind.String)
            return Task.FromResult(CreateErrorResponse(null, InvalidRequest, "Invalid Request"));

        JsonNode? requestId = obj["id"]?.DeepClone();
        string method = obj["method"]!.GetValue<string>();

        // Method dispatch is not implemented yet; every method is reported as unknown.
        return Task.FromResult(CreateErrorResponse(requestId, MethodNotFound, $"Method not found: {method}"));
    }

    private static string CreateErrorResponse(JsonNode? id, int code, string message)
        => new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
        }.ToJsonString();
}
