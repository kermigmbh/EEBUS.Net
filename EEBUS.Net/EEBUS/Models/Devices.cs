namespace EEBUS.Models
{
	public delegate void StateChangedHandler( Connection.EState state, RemoteDevice device );
	public delegate void RemoteDeviceFoundHandler( RemoteDevice device );

	public class Devices
	{
		public Devices()
		{
		}

		public LocalDevice		  Local  { get; private set; }
		public List<RemoteDevice> Remote { get; set; } = [];

		private object mutex = new();

		
		public event RemoteDeviceFoundHandler RemoteDeviceFound;
		public event StateChangedHandler	  ServerStateChanged;
		public event StateChangedHandler	  ClientStateChanged;

		protected virtual void FireRemoteDeviceFound( RemoteDevice device )
		{
			RemoteDeviceFound?.Invoke( device );
		}

		public LocalDevice GetOrCreateLocal( byte[] ski, DeviceSettings settings )
		{
			lock ( this.mutex )
			{
				if ( null == Local )
					Local = new LocalDevice( ski, settings );
			
				return Local;
			}
		}

		public RemoteDevice GetOrCreateRemote( string id, string ski, string url, string name )
		{
			if ( null != Local && Local.SKI == new SKI( ski ) )
				return null;

			RemoteDevice? remote	  = null;
			bool		 foundNew = false;

			lock ( this.mutex )
			{
				remote = Remote.FirstOrDefault( r => r.Id == id );

				if ( null == remote )
				{
					remote = new RemoteDevice( id, ski, url, name, FireServerStateChanged, FireClientStateChanged );
					Remote.Add( remote );
					foundNew = true;
				}
			}

			if ( foundNew )
				FireRemoteDeviceFound( remote );

			return remote;
		}

		public RemoteDevice? GetRemote( string id )
		{
			lock ( this.mutex )
			{
				return Remote.FirstOrDefault( r => r.Id == id );
			}
		}

		public void FireServerStateChanged( Connection.EState state, RemoteDevice device )
		{
			ServerStateChanged?.Invoke( state, device );
		}

		public void FireClientStateChanged(Connection.EState state, RemoteDevice device)
		{
			ClientStateChanged?.Invoke( state, device );
		}

		public void GarbageCollect()
		{
			lock ( this.mutex )
			{
				foreach( RemoteDevice remote in Remote.ToArray() )
				{
					if ( remote.OlderThan( new TimeSpan( 1, 0, 0 ) ) )
						Remote.Remove( remote );
				}
			}
		}
	}
}
