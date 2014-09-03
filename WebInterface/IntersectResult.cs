using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Loowoo.LandAnalyst.WebInterface
{
    [Serializable]
    public class IntersectRecord
    {
        public string DLBM { get; set; }
        public string ZLBM { get; set; }
        public double Value { get; set; }
    }
}
