using EEBUS.SPINE.Commands;

namespace EEBUS.Models
{
	public class RemoteDevice : Device
	{
		public RemoteDevice( string id, string ski, string url, string name, StateChangedHandler serverStateChanged, StateChangedHandler clientStateChanged )
			: base( "?", ski )
		{
			this.Id   = id;
			this.Url  = url;
			this.Name = name;

			this.serverStateChanged = serverStateChanged;
			this.clientStateChanged = clientStateChanged;
		}


		private StateChangedHandler	serverStateChanged;
		private StateChangedHandler	clientStateChanged;

		public Connection.EState	serverState = Connection.EState.Disconnected;
		public Connection.EState	clientState = Connection.EState.Disconnected;

		public string				Id  { get; private set; }
		public string				Url { get; private set; }
		private DateTime			Age = DateTime.UtcNow;


		public void SetServerState( Connection.EState state )
		{
			this.serverState = state;
			this.serverStateChanged( this.serverState, this );
		}

		public void SetClientState( Connection.EState state )
		{
			this.clientState = state;
			this.clientStateChanged( this.clientState, this );
		}

		public void ReNewAge()
		{
			this.Age = DateTime.UtcNow;
		}

		public bool OlderThan( TimeSpan delay )
		{
			return this.Age < DateTime.UtcNow.Add( -delay );
		}
	}
}
