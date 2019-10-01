namespace LogicApps.LogicApps.CodeGenerators
{
    using Newtonsoft.Json.Linq;
    using System.Collections.Generic;

    class ComposeCodeGenerator : ActionCodeGenerator
    {
        public override ActionType ActionType => ActionType.Inline;

        protected override IEnumerable<string> OnGenerateStatements(JToken input, ExpressionContext context)
        {
            string jsonStringLiteral;
            if (input.Type == JTokenType.String)
            {
                jsonStringLiteral = ExpressionCompiler.ConvertStringToStringInterpolation((string)input, context);
            }
            else
            {
                jsonStringLiteral = ExpressionCompiler.ConvertJTokenToStringInterpolation(input, context);
            }

            yield return $"return JToken.Parse({jsonStringLiteral});";
        }
    }
}