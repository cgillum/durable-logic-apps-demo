namespace LogicApps.LogicApps.CodeGenerators
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    class BindingCodeGenerator : ActionCodeGenerator
    {
        public override ActionType ActionType => ActionType.Activity;

        public override IEnumerable<string> GenerateStatements(JToken inputs)
        {
            string outputBindingName = inputs["name"].ToString();
            string code = $@"                
                {outputBindingName} = {ExpressionCompiler.ConvertJTokenToStringInterpolation(inputs["content"])};
                return {outputBindingName};";

            return code.Trim().Split(Environment.NewLine).Select(line => line.Trim());
        }
    }
}
