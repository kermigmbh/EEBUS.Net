using EEBUS.Models;
using EEBUS.SPINE.Commands;
using EEBUS.UseCases.ControllableSystem;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Xml;

namespace EEBUS.DataStructures
{
	public class LoadControlLimitDataStructure : DataStructure
	{
		public LoadControlLimitDataStructure( string direction, long value, short scale, string duration, bool active )
			: base( "LoadControlLimit" )
		{
			this.limitId		= 0;
			this.limitType		= "signDependentAbsValueLimit";
			this.limitCategory	= "obligation";
			this.measurementId	= 0;
			this.scopeType		= "activePowerLimit";

			this.LimitDirection	= direction;
			this.LimitChangable	= true;
			this.LimitActive	= active;
			this.EndTime		= duration;
			this.Number			= value;
			this.Scale			= scale;
		}

		private uint   limitId;
		private string limitType;
		private string limitCategory;
		private uint   measurementId;
		private string scopeType;

		public string  LimitDirection { get; set; }
		public bool	   LimitChangable { get; set; }
		public bool	   LimitActive	  { get; set; }
		public string  EndTime		  { get; set; }
		public long	   Number		  { get; set; }
		public short   Scale		  { get; set; }

		public override uint Id
		{
			get
			{
				return this.limitId;
			}
			set
			{
				this.limitId = value;
			}
		}

		public LoadControlLimitDescriptionDataType DescriptionData
		{
			get
			{
				LoadControlLimitDescriptionDataType descriptionData = new();

				descriptionData.limitId		   = this.limitId;
				descriptionData.limitType	   = this.limitType;
				descriptionData.limitCategory  = this.limitCategory;
				descriptionData.limitDirection = this.LimitDirection;
				descriptionData.measurementId  = this.measurementId;
				descriptionData.scopeType	   = this.scopeType;

				return descriptionData;
			}
		}

		public LoadControlLimitDataType Data
		{
			get
			{
				LoadControlLimitDataType data = new();

				data.limitId			= this.limitId;
				data.isLimitChangeable	= this.LimitChangable;
				data.isLimitActive		= this.LimitActive;
				data.timePeriod.endTime	= this.EndTime;
				data.value.number		= this.Number;
				data.value.scale        = this.Scale;

				return data;
			}
		}

		public override async Task SendEventAsync( Connection connection )
		{
			if ( this.LimitDirection == "consume" )
			{
				List<LPCEvents> lpcEvents = connection.Local.GetUseCaseEvents<LPCEvents>();
				foreach (var lpc in lpcEvents)
				{
					await lpc.DataUpdateLimitAsync(0, this.LimitActive, this.Number, this.EndTime == null ?  Timeout.InfiniteTimeSpan : XmlConvert.ToTimeSpan(this.EndTime ));
				}
			}
			else if (this.LimitDirection == "produce")
			{
				List<LPPEvents> lppEvents = connection.Local.GetUseCaseEvents<LPPEvents>();
				foreach (var lpp in lppEvents)
				{
					await lpp.DataUpdateLimitAsync(0, this.LimitActive, this.Number, this.EndTime == null ? Timeout.InfiniteTimeSpan : XmlConvert.ToTimeSpan(this.EndTime));
				}
			}
		}
	}
}
