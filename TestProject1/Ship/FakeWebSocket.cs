using System.Net.WebSockets;

namespace TestProject1.Ship
{
    /// <summary>
    /// Minimales In-Memory-WebSocket für Unit-Tests.
    /// Empfangene Nachrichten können mit <see cref="EnqueueReceive"/> vorprogrammiert werden.
    /// Gesendete Nachrichten stehen in <see cref="SentMessages"/>.
    /// </summary>
    internal class FakeWebSocket : WebSocket
    {
        private readonly Queue<byte[]> _receiveQueue = new();

        /// <summary>Alle Byte-Arrays, die via SendAsync übergeben wurden (in Reihenfolge).</summary>
        public List<byte[]> SentMessages { get; } = new();

        private WebSocketState _state = WebSocketState.Open;

        /// <summary>Stellt eine Nachricht bereit, die beim nächsten ReceiveAsync zurückgegeben wird.</summary>
        public void EnqueueReceive(byte[] data) => _receiveQueue.Enqueue(data);

        // ──────────────── abstrakte Properties ────────────────
        public override WebSocketState        State                  => _state;
        public override WebSocketCloseStatus? CloseStatus            => null;
        public override string?               CloseStatusDescription => null;
        public override string?               SubProtocol            => null;

        // ──────────────── abstrakte Methoden ──────────────────
        public override void Abort() => _state = WebSocketState.Aborted;

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string?              statusDescription,
            CancellationToken    cancellationToken)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string?              statusDescription,
            CancellationToken    cancellationToken)
        {
            _state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken  cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_receiveQueue.Count == 0)
                // Kein Eintrag: signalisiert ein normales WebSocket-Close
                return Task.FromResult(new WebSocketReceiveResult(
                    0,
                    WebSocketMessageType.Close,
                    true,
                    WebSocketCloseStatus.NormalClosure,
                    "No messages queued"));

            byte[] data  = _receiveQueue.Dequeue();
            int    count = Math.Min(data.Length, buffer.Count);
            Buffer.BlockCopy(data, 0, buffer.Array!, buffer.Offset, count);
            return Task.FromResult(
                new WebSocketReceiveResult(count, WebSocketMessageType.Binary, true));
        }

        public override Task SendAsync(
            ArraySegment<byte>   buffer,
            WebSocketMessageType messageType,
            bool                 endOfMessage,
            CancellationToken    cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] copy = new byte[buffer.Count];
            Buffer.BlockCopy(buffer.Array!, buffer.Offset, copy, 0, buffer.Count);
            SentMessages.Add(copy);
            return Task.CompletedTask;
        }

        public override void Dispose() { /* nichts freizugeben */ }
    }

    /// <summary>
    /// Variante, deren ReceiveAsync sofort eine <see cref="OperationCanceledException"/> wirft –
    /// simuliert das Feuern eines CMI_TIMEOUT-Timers.
    /// </summary>
    internal sealed class CancellingWebSocket : FakeWebSocket
    {
        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken  cancellationToken)
            => Task.FromException<WebSocketReceiveResult>(
                   new OperationCanceledException("CMI_TIMEOUT simulation"));
    }
}
