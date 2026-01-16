using EEBUS.SHIP.Messages;
using Makaretu.Dns;
using Microsoft.AspNetCore.SignalR.Protocol;
using System.Diagnostics;
using System.Threading.Tasks;

namespace EEBUS
{
	public class DataMessageQueue : Queue<DataMessage>
	{
		public DataMessageQueue( Connection connection )
		{
			this.connection   = connection;
			this.signalEvent  = new AutoResetEvent( false );
			this.workerThread = new Thread( () => {
				while ( true )
				{
					signalEvent.WaitOne();
					WorkerAction().GetAwaiter().GetResult();
				}
			} );
			this.workerThread.Start();
		}

		private object		   mutex = new();

		private Connection	   connection;
		private Thread		   workerThread;
		private AutoResetEvent signalEvent;

		public void Push( DataMessage message )
		{
			lock( this.mutex )
			{
				Enqueue( message );
			}

			this.signalEvent.Set();
		}

		private async Task WorkerAction()
		{
			DataMessage message;

			lock ( this.mutex )
			{
				if ( 0 == Count )
					return;

				message = Dequeue();
			}
			try
			{
                await message.Send(this.connection.WebSocket).ConfigureAwait(false);
            }
			catch (Exception ex)
			{

				Debug.WriteLine( ex.ToString());
			}
			
		}
	}
}
