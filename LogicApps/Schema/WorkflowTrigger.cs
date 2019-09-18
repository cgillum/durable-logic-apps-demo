using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogicApps.Schema
{
    internal class WorkflowTrigger
    {
        [JsonProperty("inputs")]
        public JToken Inputs { get; set; }

        [JsonProperty("recurrence")]
        public WorkflowRecurrence Recurrence { get; set; }

        [JsonProperty("type")]
        public WorkflowTriggerType Type { get; private set; }
    }
}
