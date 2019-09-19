using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace LogicApps.Schema
{
    public class WorkflowRecurrence
    {
        [JsonProperty("frequency")]
        public string Frequency { get; set; }

        [JsonProperty("interval")]
        public int Interval { get; set; }
    }
}
