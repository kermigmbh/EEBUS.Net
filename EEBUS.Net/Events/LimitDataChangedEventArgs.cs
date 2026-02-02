using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.Events
{
    public class LimitDataChangedEventArgs( ) : EventArgs
    {
        public bool IsLPC { get; init; }  
        public bool IsActive { get; init; }  
        public long Limit { get; init; } 
        public TimeSpan Duration { get; init; }

        public override string ToString()
        {
            return $"LimitDataChangedEventArgs: IsLPC={IsLPC}, IsActive={IsActive}, Limit={Limit}, Duration={Duration}";
        }
    }

}
