using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace LogicApps.Emulation
{
    internal class WorkflowContext
    {
        private readonly Dictionary<string, JToken> outputs = new Dictionary<string, JToken>();
        private readonly Dictionary<string, JToken> variables = new Dictionary<string, JToken>();
        private readonly Dictionary<string, JToken> items = new Dictionary<string, JToken>();

        public WorkflowContext(IDurableOrchestrationContext durableContext)
        {
            this.OrchestrtionContext = durableContext ?? throw new ArgumentNullException(nameof(durableContext));
        }

        public IDurableOrchestrationContext OrchestrtionContext { get; }

        public JToken GetExpandedValue(JObject jsonObject, string fieldName)
        {
            return ExpressionEvaluator.Expand(jsonObject[fieldName], this.outputs, this.variables, this.items);
        }

        public T GetExpandedValue<T>(JObject jsonObject, string fieldName)
        {
            return this.GetExpandedValue(jsonObject, fieldName).Value<T>();
        }

        public JToken GetExpandedJToken(JToken jToken)
        {
            return ExpressionEvaluator.Expand(jToken, this.outputs, this.variables, this.items);
        }

        public JToken SaveOutput(string actionName, JToken outputValue)
        {
            this.outputs.Add(actionName, outputValue);
            return outputValue;
        }

        public void SaveVariable(string variableName, JToken variableValue)
        {
            this.variables.Add(variableName, variableValue);
        }

        public void UpdateVariable(string variableName, JToken variableValue)
        {
            this.variables[variableName] = variableValue;
        }

        public T GetVariableValue<T>(string variableName)
        {
            if (!this.variables.TryGetValue(variableName, out JToken variable))
            {
                throw new ArgumentException($"Couldn't find any variable named '{variableName}'. Existing variableName: {string.Join(", ", variables.Keys)}");
            }

            string typeString = (string)variable["type"];
            Type type;
            switch (typeString)
            {
                case "Integer":
                    type = typeof(int);
                    break;
                case "String":
                    type = typeof(string);
                    break;
                case "Boolean":
                    type = typeof(bool);
                    break;
                default:
                    throw new ArgumentException($"Didn't recognize type: {typeString}.");
            }

            if (type != typeof(T))
            {
                throw new ArgumentException($"Requested type does not match stored variable type: Requested type '{typeof(T)}'. Stored type: {type}");
            }

            return variable["value"].ToObject<T>();
        }

        public void UpdateItem(string itemName, JToken item)
        {
            this.items[itemName] = item;
        }
    }
}
