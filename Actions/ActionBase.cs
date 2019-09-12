using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LogicAppsTesting.Schema;
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

    class ActionOrchestrator
    {
        public static ValueTask<JToken> ExecuteAsync(WorkflowAction workflowAction, WorkflowContext context)
        {
            var action = ActionFactory.CreateAction(workflowAction.Type);
            return action.ExecuteAsync(workflowAction.Inputs, context);
        }
    }

    public static class ActionFactory
    {
        // We'll add more to this dictionary as we add more actions
        public static Dictionary<WorkflowActionType, ActionBase> actionMap = new Dictionary<WorkflowActionType, ActionBase>
        {
            { WorkflowActionType.Compose, new ComposeAction() },
            { WorkflowActionType.Http, new HttpAction() },
            { WorkflowActionType.InitializeVariable, new InitializeVariableAction() },
            { WorkflowActionType.IncrementVariable, new IncrementVariableAction() },
         };

        public static ActionBase CreateAction(WorkflowActionType type)
        {
            return actionMap[type];
        }
    }

    public class ComposeAction : ActionBase
    {
        public override ValueTask<JToken> ExecuteAsync(JToken input, WorkflowContext context)
        {              
            return new ValueTask<JToken>(RecursiveCompose(input, context));
        }

        private JToken RecursiveCompose(JToken input, WorkflowContext context)
        {
            if (input.Type == JTokenType.Property)
            {
                JProperty jProperty = ((JProperty)input);
                if (jProperty.HasValues)
                {
                    return new JProperty(jProperty.Name, RecursiveCompose(jProperty.Value, context));
                }
                else
                {
                    return new JProperty(jProperty.Name, null);
                }
            }
            else if (input.Type == JTokenType.Object)
            {
                JObject resultObect = new JObject();
                foreach (var token in input)
                {
                    resultObect.Add(RecursiveCompose(token, context));
                }
                return resultObect;
            }
            else if (input.Type == JTokenType.Array)
            {
                JArray resultArray = new JArray();
                foreach (var token in input)
                {
                    resultArray.Add(RecursiveCompose(token, context));
                }
                return resultArray;
            }
            else
            {
                // reach the bottom layer and should expand the value
                return context.GetExpandedJToken(input);
            }
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
