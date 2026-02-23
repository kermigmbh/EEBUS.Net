namespace EEBUS.Models
{
	public class SKI
	{
		public SKI( byte[] ski )
		{
			this.ski = ski;
		}

		public SKI( string ski )
		{
			ski = ski.Replace( " ", "" );
			this.ski = Enumerable.Range( 0, ski.Length / 2 )
								 .Select( x => Convert.ToByte( ski.Substring( x * 2, 2 ), 16 ) )
								 .ToArray();
		}

		private byte[] ski;

		public override string ToString()
		{
			return Convert.ToHexString( this.ski ).ToLowerInvariant();
		}

		public string ToReadable()
		{
			string readable = ToString();

			for ( int i = 4; i < readable.Length; i += 4 )
				readable = readable.Insert( i++, " " );

			return readable;
		}

		static public bool operator ==( SKI left, SKI right )
		{
			if ( left.ski.Length != right.ski.Length )
				return false;

			for ( int i = 0; i < left.ski.Length; i++ )
				if ( left.ski[i] != right.ski[i] )
					return false;

			return true;
		}

		static public bool operator !=( SKI left, SKI right )
		{
			return ! (left == right);
		}

		static public bool operator <( SKI left, SKI right )
		{
			if ( left.ski.Length < right.ski.Length )
				return true;
			else if ( left.ski.Length > right.ski.Length )
				return false;

			for ( int i = 0; i < left.ski.Length; i++ )
			{
				if ( left.ski[i] < right.ski[i] )
					return true;
				else if ( left.ski[i] > right.ski[i] )
					return false;
			}

			return false;
		}

		static public bool operator >( SKI left, SKI right )
		{
			if ( left.ski.Length > right.ski.Length )
				return true;
			else if ( left.ski.Length < right.ski.Length )
				return false;

			for ( int i = 0; i < left.ski.Length; i++ )
			{
				if ( left.ski[i] > right.ski[i] )
					return true;
				else if ( left.ski[i] < right.ski[i] )
					return false;
			}

			return false;
		}

		public override bool Equals( object? obj )
		{
			SKI? other = obj as SKI;
			if ( other == null )
				return false;

			return this == ((SKI) obj);
		}

		public override int GetHashCode()
		{
			int hash = 0;
			foreach ( byte b in ski )
				hash += b;

			return hash;
		}
	}
}
