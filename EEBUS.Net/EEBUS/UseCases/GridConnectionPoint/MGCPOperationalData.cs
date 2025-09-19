using EEBUS.Models;

namespace EEBUS.Net.EEBUS.UseCases.GridConnectionPoint
{
	public class MGCPOperationalData : OperationalData
	{
		public long   PowerTotal	  { get; set; }		// [W]
		public long   GridFeedIn	  { get; set; }		// [Wh]
		public long   GridConsumption { get; set; }     // [Wh]
		public long[] Current		  { get; set; }     // [A, 3x]
		public long[] Voltage		  { get; set; }     // [V, 3x]
		public long	  Frequency		  { get; set; }     // [Hz]

		public void FillRandom()
		{
			Random rnd = new Random();

			PowerTotal = 4000 + rnd.Next( 1001 );

			if ( 0 == GridFeedIn )
				GridFeedIn = -2500000;
			if ( 0 == GridConsumption )
				GridConsumption = 5000000;

			GridFeedIn		-= rnd.Next( 21 );
			GridConsumption += rnd.Next( 11 );

			int aPart = rnd.Next( 101 );
			int bPart = rnd.Next( 101 - aPart );
			int cPart = 100 - aPart - bPart;

			int aVoltage = 227 + rnd.Next( 7 );
			int bVoltage = 227 + rnd.Next( 7 );
			int cVoltage = 227 + rnd.Next( 7 );

			Current = [
				(long) Math.Round( 1.0 * PowerTotal * aPart / aVoltage / 100 ),
				(long) Math.Round( 1.0 * PowerTotal * bPart / bVoltage / 100 ),
				(long) Math.Round( 1.0 * PowerTotal * cPart / cVoltage / 100 )
			];

			Voltage = [aVoltage, bVoltage, cVoltage];

			Frequency = 48 + rnd.Next( 5 );
		}
	}
}
