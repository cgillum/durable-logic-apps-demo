using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogicApps.Schema
{
    public class WorkflowTrigger
    {
        [JsonProperty("inputs")]
        public JObject Inputs { get; set; }

        [JsonProperty("recurrence")]
        public WorkflowRecurrence Recurrence { get; set; }

        [JsonProperty("type")]
        public WorkflowActionType Type { get; private set; }

        [JsonProperty("kind")]
        public WorkflowActionKind Kind { get; private set; }
    }
}
