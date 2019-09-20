namespace LogicApps.LogicApps.CodeGenerators
{
    using global::LogicApps.Emulation;
    using Newtonsoft.Json.Linq;
    using System.Collections.Generic;

    class ApiConnectionCodeGenerator : ActionCodeGenerator
    {
        // TODO: Change this to ActivityType.Inline and use the Durable HTTP feature
        public override ActionType ActionType => ActionType.Http;

        public override IEnumerable<string> GenerateStatements(JToken inputs)
        {
            // yield return "throw new NotImplementedException();";

            IList<string> statements = new List<string>();

            //string url = GetApiConnectionUrl(inputs);
            //string method = (string)inputs["method"];
            //string content = ExpressionCompiler.ConvertToStringInterpolation(inputs["body"]);

            statements.Add($@"ManagedIdentityTokenSource managedIdentityTokenSource = new ManagedIdentityTokenSource(""https://management.core.windows.net"");" +
                $@"string connection = {ExpressionCompiler.ConvertToStringInterpolation(inputs["host"]["connection"]["name"])};");
            statements.Add($@"string connection = {ExpressionCompiler.ConvertToStringInterpolation(inputs["host"]["connection"]["name"])};");
            statements.Add($@"string path = {ExpressionCompiler.ConvertToStringInterpolation(inputs["path"])};");
            statements.Add($@"string url = $""https://management.azure.com{{connection}}/extensions/proxy{{path}}?api-version=2018-07-01-preview"";");
            statements.Add($@"string method = {ExpressionCompiler.ConvertToStringInterpolation(inputs["method"])};");
            statements.Add($@"string content = {ExpressionCompiler.ConvertToStringInterpolation(inputs["body"])};");
            statements.Add($@"DurableHttpRequest request = new DurableHttpRequest(new HttpMethod(method), new Uri(url), BaseRequestHeaders, content: content, tokenSource: managedIdentityTokenSource);");
            statements.Add($"DurableHttpResponse response = await context.OrchestrtionContext.CallHttpAsync(request);");

            return statements;

        }
        private string GetApiConnectionUrl(JToken inputs)
        {
            var connection = ExpressionCompiler.ParseToken(inputs["host"]["connection"]["name"]);
            var path = ExpressionCompiler.ParseToken(inputs["path"]);
            var url = $@"https://management.azure.com{{{connection}}}/extensions/proxy{{path}}?api-version=2018-07-01-preview";
            return url;
            //return $"https://management.azure.com{connection}/extensions/proxy{path}?api-version=2018-07-01-preview";
        }
    }
}
