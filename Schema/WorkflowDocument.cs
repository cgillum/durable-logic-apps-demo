using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace LogicAppsTesting.Schema
{
    class WorkflowDocument
    {
        [JsonProperty("definition")]
        public WorkflowDefinition Definition { get; set; }
    }
}
