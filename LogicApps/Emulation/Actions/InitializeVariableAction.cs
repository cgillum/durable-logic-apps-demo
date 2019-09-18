using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LogicApps.Emulation.Actions
{
    internal class InitializeVariableAction : ActionBase
    {
        public override ValueTask<JToken> ExecuteAsync(JToken input, WorkflowContext context)
        {
            JObject requestInputs = (JObject)input;
            JArray result = new JArray();

            foreach (JObject variable in requestInputs["variables"])
            {
                // evaluate the expression 
                string variableName = context.GetExpandedValue<string>(variable, "name");
                string variableType = context.GetExpandedValue<string>(variable, "type");
                int variableValue = context.GetExpandedValue<int>(variable, "value");

                // create the variable with the expanded alue
                JToken storedVariable = new JObject(
                    new JProperty("name", variableName),
                    new JProperty("type", variableType),
                    new JProperty("value", variableValue));

                // store the variable into workflow context
                context.SaveVariable(variableName, storedVariable);
                result.Add(storedVariable);
            }

            return new ValueTask<JToken>(result);
        }
    }
}
