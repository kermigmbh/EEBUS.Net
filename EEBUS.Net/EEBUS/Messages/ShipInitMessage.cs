using EEBUS.Enums;

namespace EEBUS.Messages
{
	public abstract class ShipInitMessage<T> : ShipMessage<T> where T : ShipInitMessage<T>, new()
	{
		public new abstract class Class : ShipMessage<T>.Class
		{
		}

		protected override byte GetDataType()
		{
			return SHIPMessageType.INIT;
		}

		public abstract byte[] bytes { get;  set; }

		protected override byte[] ToJson()
		{
			return new byte[] { SHIPMessageValue.CMI_HEAD };
		}

		public override T FromJsonVirtual(ReadOnlySpan<byte> data, Connection connection )
		{
			return FromJson( data, connection );
		}

		static new public T FromJson(ReadOnlySpan<byte> data, Connection connection )
		{
			T init = new T();

			if ( data == null || data.Length == 0 )
				return null;
			else if ( data.Length == 1 )
				init.bytes[1] = data[0];
			else
			{
				init.bytes[0] = data[0];
				init.bytes[1] = data[1];
			}

			return init;
		}
	}
}
