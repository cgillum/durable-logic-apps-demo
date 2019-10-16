namespace LogicApps.CodeGenerators
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json.Linq;

    class BindingCodeGenerator : ActionCodeGenerator
    {
        public override ActionType ActionType => ActionType.Activity;

        protected override IEnumerable<string> OnGenerateStatements(JToken inputs, ExpressionContext context)
        {
            string outputBindingName = inputs["name"].ToString();
            string code = $@"                
                {outputBindingName} = {ExpressionCompiler.ConvertJTokenToStringInterpolation(inputs["content"], context)};
                return {outputBindingName};";

            return code.Trim().Split(Environment.NewLine).Select(line => line.Trim());
        }
    }
}
