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

            // REVIEW: Do we need to consider the "type" field?
            yield return $"JToken {name} = {variable["value"].ToString()};";
        }
    }
}
