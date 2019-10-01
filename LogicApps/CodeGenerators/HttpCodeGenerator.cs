namespace LogicApps.LogicApps.CodeGenerators
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    class HttpCodeGenerator : ActionCodeGenerator
    {
        public override ActionType ActionType => ActionType.Http;

        protected override IEnumerable<string> OnGenerateStatements(JToken input, ExpressionContext context)
        {
            JObject requestInputs = (JObject)input;

            // TODO: Expression parsing and expansion
            JToken body = requestInputs["body"];
            string method = (string)requestInputs["method"];
            string uri = (string)requestInputs["uri"];

            // TODO: Add support for user-defined headers
            string code = $@"
                var method = new HttpMethod(""{method}"");
                var uri = new Uri(""{uri}"");
                var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
                headers.Add(""Content-Type"", ""application/json"");
                var content = {ExpressionCompiler.ConvertJTokenToStringInterpolation(body, context)};
                var request = new DurableHttpRequest(method, uri, headers, content);
                DurableHttpResponse response = await context.CallHttpAsync(request);
                return new JObject {{ {{ ""statusCode"", (int)response.StatusCode }}, {{ ""headers"", JObject.FromObject(response.Headers) }}, {{ ""body"", response.Content }} }};
                ";

            // Strip leading and trailing whitespace for the code block and each individual line
            return code.Trim().Split(Environment.NewLine).Select(line => line.Trim());
        }
    }
}
