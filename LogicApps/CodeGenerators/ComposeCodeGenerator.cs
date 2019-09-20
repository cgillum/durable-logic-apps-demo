using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LogicApps.LogicApps.CodeGenerators
{
    class ComposeCodeGenerator : ActionCodeGenerator
    {
        public override ActionType ActionType => ActionType.Inline;

        public override IEnumerable<string> GenerateStatements(JToken input)
        {
            // TODO: Need to expand inline functions
            string jsonStringLiteral = Utils.CreateJsonStringLiteral(input);
            yield return $"return JToken.Parse({jsonStringLiteral});";
        }
    }
}
