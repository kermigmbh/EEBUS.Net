using EEBUS.Enums;
using EEBUS.Messages;
using System.Net.WebSockets;
using System.Threading;
using static EEBUS.Connection;

namespace EEBUS
{
    public class EEBusWebSocket
    {
        public EEBusWebSocket(WebSocket ws)
        {
            this._ws = ws;

        }

        private WebSocket? _ws;

        private byte[] _receiveBuffer = new byte[10240];

        public WebSocketState SocketState
        {
            get

            {
                return _ws?.State ?? WebSocketState.None;
            }
        }

        public async Task<ShipMessageBase?> ReceiveAsync(CancellationToken cancellationToken)
        {
            int totalCount = 0;
            WebSocketReceiveResult result;

            //using CancellationTokenSource timeoutCts = new CancellationTokenSource(SHIPMessageTimeout.CMI_TIMEOUT);
            //using CancellationTokenSource linkedTokenSource =
            //    CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            // Accumulate frames until EndOfMessage
            do
            {
                if (totalCount >= _receiveBuffer.Length)
                    throw new Exception("EEBUS payload too large for receive buffer.");

                var segment = new ArraySegment<byte>(
                    _receiveBuffer,
                    totalCount,
                    _receiveBuffer.Length - totalCount);

                result = await _ws.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);

                if (result.CloseStatus.HasValue || result.MessageType == WebSocketMessageType.Close)
                {
                    //this.state = EState.Stopped;
                    break;
                }

                totalCount += result.Count;

            } while (!result.EndOfMessage && !cancellationToken.IsCancellationRequested);

            ReadOnlySpan<byte> messageSpan = _receiveBuffer.AsSpan(0, totalCount);

            ShipMessageBase? message = ShipMessageBase.Create(messageSpan);
            return message;
        }
    }
}
