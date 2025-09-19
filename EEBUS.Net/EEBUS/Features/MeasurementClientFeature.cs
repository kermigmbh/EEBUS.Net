using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.Features
{
	public class MeasurementClientFeature : Feature
	{
		static MeasurementClientFeature()
		{
			Register( "Measurement-client", new Class() );
		}

		public MeasurementClientFeature( Entity owner )
			: base( "Measurement", "client", owner )
		{
		}

		public MeasurementClientFeature( int index, Entity owner, FeatureInformationType featureInfo )
			: base( index, "Measurement", "client", owner, featureInfo )
		{
		}

		public new class Class : Feature.Class
		{
			public override Feature Create( Entity owner )
			{
				return new MeasurementClientFeature( owner );
			}

			public override Feature Create( int index, Entity owner, FeatureInformationType featureInfo )
			{
				return new MeasurementClientFeature( index, owner, featureInfo  );
			}
		}

		public override string Description
		{
			get
			{
				return "Measurement Client";
			}
		}

		public List<MeasurementData.MeasurementData> measurementData = new();
	}
}
