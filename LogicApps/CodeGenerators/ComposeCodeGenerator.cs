namespace LogicApps.LogicApps.CodeGenerators
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System.Collections.Generic;

    class ComposeCodeGenerator : ActionCodeGenerator
    {
        public override ActionType ActionType => ActionType.Inline;

        public override IEnumerable<string> GenerateStatements(JToken inputs)
        {
            yield return $@"return {ExpressionCompiler.ConvertToStringInterpolation(inputs)};";
        }
    }
}