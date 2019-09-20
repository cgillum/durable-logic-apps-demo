namespace LogicApps.LogicApps.CodeGenerators
{
    using Newtonsoft.Json.Linq;
    using System.Collections.Generic;

    class ComposeCodeGenerator : ActionCodeGenerator
    {
        public override ActionType ActionType => ActionType.Inline;

        public override IEnumerable<string> GenerateStatements(JToken input)
        {
             //yield return $@"return {ExpressionCompiler.ConvertToStringInterpolation(inputs)};";

            // TODO: Need to expand inline functions
            // string jsonStringLiteral = Utils.CreateJsonStringLiteral(input);
            string jsonStringLiteral = ExpressionCompiler.ConvertToStringInterpolation(input);
            yield return $"return JToken.Parse({jsonStringLiteral});";
        }
    }
}