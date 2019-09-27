namespace LogicApps.LogicApps.CodeGenerators
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    class ApiConnectionOutputBindingCodeGenerator : ApiConnectionCodeGenerator
    {
        public ApiConnectionOutputBindingCodeGenerator(ApiConnectionCodeGenerator generator)
        {
        }

        public override IEnumerable<string> GenerateStatements(JToken inputs)
        {
            ExpressionCompiler.ConvertJTokenToStringInterpolation(inputs["body"]["ContentData"]);
            string code = $@"                
                return {ExpressionCompiler.ConvertJTokenToStringInterpolation(inputs["body"]["ContentData"])};
                ";

            return code.Trim().Split(Environment.NewLine).Select(line => line.Trim());
        }
    }
}
