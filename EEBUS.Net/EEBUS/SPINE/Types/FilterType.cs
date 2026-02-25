using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.SPINE.Types
{
    [System.SerializableAttribute()]
    public class FilterType
    {
        public CmdControlType? cmdControl { get; set; }
    }

    [System.SerializableAttribute()]
    public class CmdControlType
    {
        public object? delete {  get; set; }
        public object? partial { get; set; }
    }
}
