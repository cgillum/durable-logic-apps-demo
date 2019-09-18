using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace LogicApps.Emulation
{
    // TODO: There is a ton more stuff that needs to be added here:
    //       https://docs.microsoft.com/en-us/azure/logic-apps/workflow-definition-language-functions-reference
    //       Even better if we can copy/paste existing Logic Apps code for this.
    internal static class ExpressionEvaluator
    {
        public static JToken Expand(
            JToken input,
            IReadOnlyDictionary<string, JToken> outputs,
            IReadOnlyDictionary<string, JToken> variables,
            IReadOnlyDictionary<string, JToken> items)
        {
            // Check to see if this is an expression
            if (input.Type == JTokenType.String)
            {
                string stringBody = (string)input;
                if (stringBody?.Length > 0 && stringBody[0] == '@')
                {
                    return Evaluate(stringBody, outputs, variables, items);
                }
            }

            return input;
        }

        private static JToken Evaluate(
            string expression,
            IReadOnlyDictionary<string, JToken> outputs,
            IReadOnlyDictionary<string, JToken> variables,
            IReadOnlyDictionary<string, JToken> items)
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
