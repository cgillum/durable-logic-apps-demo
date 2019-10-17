using System.Collections.Generic;
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
            string csharpType;
            switch (type.ToLowerInvariant())
            {
                case "string":
                    csharpType = "string";
                    expression = "\"" + value + "\"";
                    break;
                case "array":
                    csharpType = "JArray";
                    expression = $"JArray.Parse({ExpressionCompiler.ConvertJTokenToStringInterpolation(value, context)});";
                    break;
                case "object":
                    csharpType = "JObject";
                    expression = $"JObject.Parse({ExpressionCompiler.ConvertJTokenToStringInterpolation(value, context)});";
                    break;
                case "boolean":
                    csharpType = "bool";
                    expression = value.ToString().ToLowerInvariant();
                    break;
                case "integer":
                    csharpType = "int";
                    expression = value.ToString();
                    break;
                case "float":
                    csharpType = "double";
                    expression = value.ToString();
                    break;
                default:
                    csharpType = "JToken";
                    expression = value.ToString();
                    break;
            }

            yield return $"{csharpType} {name} = {expression};";
        }
    }
}
