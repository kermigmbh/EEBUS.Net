using System.Text.Json;
using System.Text.Json.Nodes;

namespace EEBUS.Messages
{
	public abstract class SpineCmdPayloadBase
	{
		public SpineCmdPayloadBase()
		{
		}
		public abstract JsonNode? ToJsonNode();
         
        public abstract class Class
		{
			public virtual async ValueTask<SpineCmdPayloadBase> CreateAnswerAsync( DatagramType datagram, HeaderType header, Connection connection )
			{
				return null;
			}

			public virtual SpineCmdPayloadBase CreateNotify( Connection connection )
			{
				return null;
			}

			public virtual SpineCmdPayloadBase CreateRead( Connection connection )
			{
				return null;
			}

			public virtual SpineCmdPayloadBase CreateCall( Connection connection )
			{
				return null;
			}

			public virtual async ValueTask EvaluateAsync( Connection connection, DatagramType datagram )
			{
			}

			public virtual bool? GetAnswerAckRequest()
			{
				return null;
			}
		}

		static protected Dictionary<string, Class> commands = new Dictionary<string, Class>();

		static protected void Register( string cmd, Class cls )
		{
			commands.Add( cmd, cls );
		}

		static public Class GetClass( string cmd )
		{
			if ( commands.TryGetValue( cmd, out Class cls ) )
				return cls;

			return null;
		}
	}
}
