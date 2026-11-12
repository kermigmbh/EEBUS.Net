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
/// </summary>
internal sealed class RpcServer : IAsyncDisposable
{
    private const int MethodNotFound = -32601;
    private const int InvalidRequest = -32600;
    private const int ParseError = -32700;

    // No byte order mark: a leading EF BB BF would make the client's JSON parser reject the first response.
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly TcpListener _listener;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptLoop;
    private int _stopped;

    public RpcServer(int port, ILogger? logger = null)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _logger = logger;
    }

    /// <summary>The port the server is bound to. Resolves the actual port when constructed with port 0.</summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>Binds the listener and starts accepting clients. Throws if the port cannot be bound.</summary>
    public void Start()
    {
        if (_acceptLoop != null)
            throw new InvalidOperationException("RPC server has already been started.");

        try
        {
            _listener.Start();
        }
        catch
        {
            // TcpListener does not release its socket when Bind fails; clean up and mark as stopped so StopAsync is a no-op.
            Interlocked.Exchange(ref _stopped, 1);
            _listener.Dispose();
            _cts.Dispose();
            throw;
        }

        _acceptLoop = AcceptLoopAsync(_cts.Token);
        _logger?.LogInformation("RPC server listening on port {Port}.", Port);
    }

    /// <summary>
    /// Stops accepting clients and cancels open client connections. Does not wait for in-flight requests to finish.
    /// </summary>
    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 1)
            return;

        _cts.Cancel();
        _listener.Stop();

        if (_acceptLoop != null)
            await _acceptLoop.ConfigureAwait(false);

        _cts.Dispose();
        _logger?.LogInformation("RPC server stopped.");
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
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
