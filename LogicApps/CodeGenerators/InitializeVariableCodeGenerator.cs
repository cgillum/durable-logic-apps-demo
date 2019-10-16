using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogicApps.CodeGenerators
{
    class InitializeVariableCodeGenerator : ActionCodeGenerator
    {
        public override ActionType ActionType => ActionType.Inline;

        protected override IEnumerable<string> OnGenerateStatements(JToken input, ExpressionContext context)
        {
            JObject inputObject = (JObject)input;
            JArray variables = (JArray)inputObject["variables"];
            JObject variable = (JObject)variables[0]; // The schema says there is always exactly one element
            string name = (string)variable["name"];
            string type = (string)variable["type"];
            JToken value = variable["value"];

            string expression;
            switch (type.ToLowerInvariant())
            {
                case "string":
                    expression = "\"" + value + "\"";
                    break;
                case "array":
                    expression = $"JArray.Parse(@\"{value.ToString(Formatting.None).Replace("\"", "\"\"")}\")";
                    break;
                case "boolean":
                    expression = value.ToString().ToLowerInvariant();
                    break;
                default:
                    expression = value.ToString();
                    break;
            }

            yield return $"JToken {name} = {expression};";
        }
    }
}
