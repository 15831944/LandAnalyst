using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Loowoo.LandAnalyst.WebInterface
{
    [Serializable]
    public class ResultPack
    {
        public string ErrorMessage { get; set; }
        public string ImageContent { get; set; }
        public List<IntersectRecord> Details { get; set; }
        public ResultPack()
        {
            Details = new List<IntersectRecord>();
        }
    }
}