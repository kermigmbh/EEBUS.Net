using EEBUS.Models;
using EEBUS.SPINE.Commands;
using EEBUS.UseCases.ControllableSystem;
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
		public string?  EndTime		  { get; set; }
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

		public void Update(LoadControlLimitDataType data)
		{
            //this.LimitChangable = data.isLimitChangeable ?? this.LimitChangable;	not changeable according to the LPC definition
            this.LimitActive = data.isLimitActive ?? this.LimitActive;

			this.EndTime = data.timePeriod?.endTime;	//timePeriod is optional according to the LPC definition
				
			this.Number = data.value?.number ?? this.Number;
			this.Scale = data.value?.scale ?? this.Scale;
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

				if (this.EndTime != null)
				{
                    data.timePeriod = new()
                    {
                        endTime = this.EndTime
                    };
                }

                data.value = new()
                {
                    number = this.Number,
                    scale = this.Scale
                };

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
					await lpc.DataUpdateLimitAsync(0, this.LimitActive, this.Number, this.EndTime == null ?  Timeout.InfiniteTimeSpan : XmlConvert.ToTimeSpan(this.EndTime ), connection.Remote?.SKI.ToString() ?? string.Empty);
				}
			}
			else if (this.LimitDirection == "produce")
			{
				List<LPPEvents> lppEvents = connection.Local.GetUseCaseEvents<LPPEvents>();
				foreach (var lpp in lppEvents)
				{
					await lpp.DataUpdateLimitAsync(0, this.LimitActive, this.Number, this.EndTime == null ? Timeout.InfiniteTimeSpan : XmlConvert.ToTimeSpan(this.EndTime), connection.Remote?.SKI.ToString() ?? string.Empty);
				}
			}
		}
	}
}
