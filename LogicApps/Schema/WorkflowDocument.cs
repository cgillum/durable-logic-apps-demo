using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace LogicApps.Schema
{
    public class WorkflowDocument
    {
        [JsonProperty("$connections")]
        public JObject ConnectionToken { get; set; }

        [JsonProperty("definition")]
        public WorkflowDefinition Definition { get; set; }
    }
}
