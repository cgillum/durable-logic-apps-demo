using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace LogicAppsTesting
{
    public class WorkflowContext
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
            if(!this.variables.TryGetValue(variableName, out JToken variable))
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

    // TODO: There is a ton more stuff that needs to be added here:
    //       https://docs.microsoft.com/en-us/azure/logic-apps/workflow-definition-language-functions-reference
    //       Even better if we can copy/paste existing Logic Apps code for this.
    static class ExpressionEvaluator
    {
        public static JToken Expand(JToken input, IReadOnlyDictionary<string, JToken> outputs, IReadOnlyDictionary<string, JToken> variables, IReadOnlyDictionary<string, JToken> items)
        {
            // Check to see if this is an expression
            if (input.Type == JTokenType.String)
            {
                string stringBody = (string)input;
                if (stringBody.StartsWith('@'))
                {
                    return Evaluate(stringBody, outputs, variables, items);
                }
            }

            return input;
        }

        private static JToken Evaluate(string expression, IReadOnlyDictionary<string, JToken> outputs, IReadOnlyDictionary<string, JToken> variables, IReadOnlyDictionary<string, JToken> items )
        {
            Match match;
            if (expression.StartsWith("@outputs("))
            {
                match = Regex.Match(expression, @"@outputs\('(\w+)'\)");
                string outputName = match.Groups[1].Value;
                if (outputs.TryGetValue(outputName, out JToken output))
                {
                    return output;
                }

                throw new ArgumentException($"Couldn't find any output named '{outputName}'. Existing outputs: {string.Join(", ", outputs.Keys)}");
            }
            else if (expression.StartsWith("@variables("))
            {
                match = Regex.Match(expression, @"@variables\('(\w+)'\)");
                string variableName = match.Groups[1].Value;
                if (variables.TryGetValue(variableName, out JToken variable))
                {
                    return variable["value"];
                }

                throw new ArgumentException($"Couldn't find any variable named '{variableName}'. Existing variableName: {string.Join(", ", variables.Keys)}");
            }
            else if (expression.StartsWith("@items("))
            {
                match = Regex.Match(expression, @"@items\('(\w+)'\)");
                string itemGroupName = match.Groups[1].Value;
                if (items.TryGetValue(itemGroupName, out JToken item))
                {
                    return item;
                }

                throw new ArgumentException($"Couldn't find any items for '{itemGroupName}'. Existing item group: {string.Join(", ", items.Keys)}");
            }
            else if (expression.StartsWith("@guid("))
            {
                return Guid.NewGuid();
            }
            else if (expression.StartsWith("@utcNow("))
            {
                return DateTime.UtcNow;
            }
            else
            {
                throw new ArgumentException($"Didn't recognize expression: {expression}.");
            }
        }
    }
}
