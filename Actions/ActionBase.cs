using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dynamitey.DynamicObjects;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;

namespace LogicAppsTesting.Actions
{
    // TODO: Move each action into its own file.
    public abstract class ActionBase
    {
        public abstract ValueTask<JToken> ExecuteAsync(JToken input, WorkflowContext context);
    }

    public class ComposeAction : ActionBase
    {
        public override ValueTask<JToken> ExecuteAsync(JToken input, WorkflowContext context)
        {
            // TODO: Actual implementation
            return new ValueTask<JToken>(new JObject(
                new JProperty("currentDateTimeUtc", DateTime.UtcNow),
                new JProperty("someId", Guid.NewGuid())));
        }
    }

    public class HttpAction : ActionBase
    {
        static readonly Dictionary<string, StringValues> BaseRequestHeaders = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
        {
            { "Content-Type", "application/json" },
        };

        public override async ValueTask<JToken> ExecuteAsync(JToken input, WorkflowContext context)
        {
            JObject requestInputs = (JObject)input;
            JToken body = context.GetExpandedValue(requestInputs, "body");
            string method = context.GetExpandedValue<string>(requestInputs, "method");
            string uri = context.GetExpandedValue<string>(requestInputs, "uri");

            var request = new DurableHttpRequest(
                new System.Net.Http.HttpMethod(method),
                new Uri(uri),
                BaseRequestHeaders,
                body.ToString(Newtonsoft.Json.Formatting.None));

            DurableHttpResponse response = await context.OrchestrtionContext.CallHttpAsync(request);

            return new JObject(
                new JProperty("statusCode", (int)response.StatusCode),
                new JProperty("headers", JObject.FromObject(response.Headers)),
                new JProperty("body", response.Content));
        }
    }
}
