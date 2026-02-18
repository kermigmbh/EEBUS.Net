using EEBUS.Messages;
using System.Text.Json.Nodes;

namespace EEBUS.Models
{
	public abstract class DataStructure
	{
		public DataStructure( string type )
		{
			Type = type;
		}

		protected string	Type { get; private set; }
		public virtual uint Id  { get; set; }

		public abstract Task SendEventAsync( Connection connection );

        public abstract Task SendNotifyAsync(LocalDevice localDevice, AddressType localAddress, JsonNode? payload);

    }
}
