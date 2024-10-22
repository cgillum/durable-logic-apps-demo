﻿namespace LogicApps.CodeGenerators
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    class ApiConnectionCodeGenerator : ActionCodeGenerator
    {
        public override ActionType ActionType => ActionType.Http;

        protected override IEnumerable<string> OnGenerateStatements(JToken inputs, ExpressionContext context)
        {
            string code = $@"                
                ManagedIdentityTokenSource managedIdentityTokenSource = new ManagedIdentityTokenSource(""https://management.core.windows.net"");
                string connection = {ExpressionCompiler.ConvertJTokenToStringInterpolation(inputs["host"]["connection"]["name"], context)};
                var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
                headers.Add(""Content-Type"", ""application/json"");
                string path = {ExpressionCompiler.ConvertJTokenToStringInterpolation(inputs["path"], context)};
                Uri uri = new Uri($""https://management.azure.com{{connection}}/extensions/proxy{{path}}?api-version=2018-07-01-preview"");
                HttpMethod method = new HttpMethod({ExpressionCompiler.ConvertJTokenToStringInterpolation(inputs["method"], context)});
                string content = {ExpressionCompiler.ConvertJTokenToStringInterpolation(inputs["body"], context)};
                DurableHttpRequest request = new DurableHttpRequest(method, uri, headers, content, tokenSource: managedIdentityTokenSource);
                DurableHttpResponse response = await context.CallHttpAsync(request);
                return new JObject {{ {{ ""statusCode"", (int)response.StatusCode }}, {{ ""headers"", JObject.FromObject(response.Headers) }}, {{ ""body"", response.Content }} }};
                ";

            return code.Trim().Split(Environment.NewLine).Select(line => line.Trim());

        }
    }
}
