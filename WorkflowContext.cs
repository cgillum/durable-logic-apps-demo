using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace LogicAppsTesting
{
    public class WorkflowContext
    {
        private readonly Dictionary<string, JToken> outputs = new Dictionary<string, JToken>();

        public WorkflowContext(IDurableOrchestrationContext durableContext)
        {
            this.OrchestrtionContext = durableContext ?? throw new ArgumentNullException(nameof(durableContext));
        }

        public IDurableOrchestrationContext OrchestrtionContext { get; }

        public JToken GetExpandedValue(JObject jsonObject, string fieldName)
        {
            return ExpressionEvaluator.Expand(jsonObject[fieldName], this.outputs);
        }

        public T GetExpandedValue<T>(JObject jsonObject, string fieldName)
        {
            return this.GetExpandedValue(jsonObject, fieldName).Value<T>();
        }

        public JToken SaveOutput(string actionName, JToken outputValue)
        {
            this.outputs.Add(actionName, outputValue);
            return outputValue;
        }
    }

    static class ExpressionEvaluator
    {
        public static JToken Expand(JToken input, IReadOnlyDictionary<string, JToken> outputs)
        {
            // Check to see if this is an expression
            if (input.Type == JTokenType.String)
            {
                string stringBody = (string)input;
                if (stringBody.StartsWith('@'))
                {
                    return Evaluate(stringBody, outputs);
                }
            }

            return input;
        }

        private static JToken Evaluate(string expression, IReadOnlyDictionary<string, JToken> outputs)
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
            else
            {
                throw new ArgumentException($"Didn't recognize expression: {expression}.");
            }
        }
    }
}
