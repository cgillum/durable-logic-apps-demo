namespace LogicApps.LogicApps.CodeGenerators
{
    using Newtonsoft.Json.Linq;
    using System.Collections.Generic;

    class ComposeCodeGenerator : ActionCodeGenerator
    {
        public override ActionType ActionType => ActionType.Inline;

        public override IEnumerable<string> GenerateStatements(JToken input)
        {
            string jsonStringLiteral;
            if (input.Type == JTokenType.String)
            {
                jsonStringLiteral = ExpressionCompiler.ConvertStringToStringInterpolation((string)input);
            }
            else
            {
                jsonStringLiteral = ExpressionCompiler.ConvertJTokenToStringInterpolation(input);
            }

            yield return $"return JToken.Parse({jsonStringLiteral});";
        }
    }
}