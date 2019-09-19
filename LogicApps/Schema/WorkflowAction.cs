using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace LogicApps.Schema
{
    public class WorkflowAction
    {
        public WorkflowAction(string name)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }

        [JsonProperty("inputs")]
        public JToken Inputs { get; private set; }

        [JsonProperty("runAfter")]
        [JsonConverter(typeof(DependencyConverter))]
        public IReadOnlyDictionary<string, IReadOnlyList<WorkflowStatus>> Dependencies { get; private set; }

        [JsonProperty("type")]
        public WorkflowActionType Type { get; private set; }

        public override string ToString()
        {
            return this.Type.ToString();
        }

        private class DependencyConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType is IReadOnlyDictionary<string, IReadOnlyList<WorkflowStatus>>;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var actionDictionary = new Dictionary<string, IReadOnlyList<WorkflowStatus>>(StringComparer.Ordinal);
                while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
                {
                    string dependantActionName = (string)reader.Value;
                    if (reader.Read())
                    {
                        var statusValues = new List<WorkflowStatus>();
                        while (reader.Read() && reader.TokenType == JsonToken.String)
                        {
                            statusValues.Add((WorkflowStatus)Enum.Parse(typeof(WorkflowStatus), (string)reader.Value));
                        }

                        actionDictionary.Add(dependantActionName, statusValues);
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
