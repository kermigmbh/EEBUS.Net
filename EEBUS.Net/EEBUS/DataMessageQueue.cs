using EEBUS.SHIP.Messages;
using System.Diagnostics;
using System.Threading.Channels;

namespace EEBUS
{
    public class DataMessageQueue
    {
        private readonly Channel<DataMessage> _channel;
        private readonly Connection _connection;
        private readonly Task _workerTask;

        public DataMessageQueue(Connection connection)
        {
            _connection = connection;

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

        public ValueTask PushAsync(DataMessage message)
        {
            return _channel.Writer.WriteAsync(message);
        }

        public void Push (DataMessage message)
        {
              bool b = _channel.Writer.TryWrite (message);

            if (!b)
            {
                Debug.WriteLine("Cannot push message");
            }
        }

        private async Task WorkerLoop()
        {
            try
            {
                await foreach (var message in _channel.Reader.ReadAllAsync())
                {
                    try
                    {
                        await message.Send(_connection.WebSocket)
                                     .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                        _channel.Writer.Complete();
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
