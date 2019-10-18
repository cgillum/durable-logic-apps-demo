using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LogicApps.CodeGenerators
{
    class IncrementVariableCodeGenerator : ActionCodeGenerator
    {
        public override ActionType ActionType => ActionType.Inline;

        protected override IEnumerable<string> OnGenerateStatements(JToken input, ExpressionContext context)
        {
            JObject inputJson = (JObject)input;
            string variableName = (string)inputJson["name"];
            if (inputJson.TryGetValue("value", out JToken jsonValue))
            {
                yield return $"{variableName} += {jsonValue};";
            }
            else
            {
                yield return $"{variableName}++;";
            }
        }
    }
}
