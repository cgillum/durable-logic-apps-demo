using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace LogicApps.Schema
{
    internal class WorkflowRecurrence
    {
        [JsonProperty("frequency")]
        public string Frequency { get; set; }

        [JsonProperty("interval")]
        public int Interval { get; set; }
    }
}
