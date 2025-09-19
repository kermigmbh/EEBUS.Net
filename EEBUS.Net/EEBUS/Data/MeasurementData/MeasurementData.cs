using EEBUS.SPINE.Commands;

namespace EEBUS.MeasurementData
{
	public class MeasurementData
	{
		public uint												measurementId								 { get; set; }
		public ElectricalConnectionParameterDescriptionDataType	electricalConnectionParameterDescriptionData { get; set; }
		public MeasurementDataType								measurementDataType							 { get; set; }
		public MeasurementDescriptionDataType					measurementDescriptionDataType				 { get; set; }
	}
}
