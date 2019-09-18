using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LogicApps.Emulation.Actions
{
    internal class ComposeAction : ActionBase
    {
        public override ValueTask<JToken> ExecuteAsync(JToken input, WorkflowContext context)
        {
            return new ValueTask<JToken>(RecursiveCompose(input, context));
        }

        private static JToken RecursiveCompose(JToken input, WorkflowContext context)
        {
            if (input.Type == JTokenType.Property)
            {
                JProperty jProperty = (JProperty)input;
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
}
