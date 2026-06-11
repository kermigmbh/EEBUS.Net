using EEBUS.SHIP.Messages;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading.Channels;

namespace EEBUS
{
    public class DataMessageQueue
    {
        private readonly Channel<DataMessage> _channel;
        private readonly Connection _connection;
        private readonly Task _workerTask;
        private ILogger? _logger;

        public DataMessageQueue(Connection connection, ILogger? logger = null)
        {
            _connection = connection;
            _logger = logger;

            // Unbounded channel, single reader, multiple writers
            _channel = Channel.CreateUnbounded<DataMessage>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });

            // Start background consumer
            _workerTask = Task.Run(WorkerLoop);
        }

        /// <summary>
        /// Marks the queue as completed. After this call, further <see cref="Push"/> /
        /// <see cref="PushAsync"/> calls will return false / throw, and the worker
        /// loop will exit once it has drained any messages already enqueued.
        /// </summary>
        public void Complete()
        {
            _channel.Writer.TryComplete();
        }

        public async ValueTask<bool> PushAsync(DataMessage message)
        {
            try
            {
                await _channel.Writer.WriteAsync(message).ConfigureAwait(false);
                return true;
            }
            catch (ChannelClosedException)
            {
                Debug.WriteLine("DataMessageQueue.PushAsync: queue is closed, message dropped");
                return false;
            }
        }

        public bool Push(DataMessage message)
        {
            if (!_channel.Writer.TryWrite(message))
            {
                // TryWrite only returns false on an unbounded channel when the
                // writer has been completed - i.e. the connection is tearing down.
                Debug.WriteLine("DataMessageQueue.Push: queue is closed, message dropped");
                return false;
            }
            return true;
        }

        private async Task WorkerLoop()
        {
            try
            {
                await foreach (var message in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
                {
                    try
                    {
                        //Debug.WriteLine("===> " + message.ToString());
                        await message.Send(_connection.WebSocket, _logger)
                                     .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // A single failed send must not kill the queue. The underlying
                        // WebSocket may be in a transient bad state, the next message
                        // may succeed, or the connection owner may explicitly Complete()
                        // the queue when it decides the connection is unrecoverable.
                        Debug.WriteLine("DataMessageQueue: send failed, continuing - " + ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WorkerLoop crashed: " + ex);
            }
        }
    }
}
