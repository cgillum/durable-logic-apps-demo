using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace LogicApps.Schema
{
    public class WorkflowDefinition
    {
        [JsonProperty("actions")]
        [JsonConverter(typeof(ActionsConverter))]
        public IDictionary<string, WorkflowAction> Actions { get; set; }

        [JsonProperty("triggers")]
        public IDictionary<string, WorkflowTrigger> Triggers { get; set; }

        private class ActionsConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType is IDictionary<string, WorkflowAction>;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var actionDictionary = new Dictionary<string, WorkflowAction>(StringComparer.Ordinal);
                while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
                {
                    string actionName = (string)reader.Value;
                    var action = new WorkflowAction(actionName);

                    if (reader.Read())
                    {
                        serializer.Populate(reader, action);
                        actionDictionary.Add(actionName, action);
                    }
                }

                return actionDictionary;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}
