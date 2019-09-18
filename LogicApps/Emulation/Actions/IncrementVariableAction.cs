using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LogicApps.Emulation.Actions
{
    internal class IncrementVariableAction : ActionBase
    {
        public override ValueTask<JToken> ExecuteAsync(JToken input, WorkflowContext context)
        {
            string variableName = (string)input["name"];
            int delta = (int)input["value"];

            // retrieved stored variable
            int variableValue = context.GetVariableValue<int>(variableName);

            // create the updated variable
            JToken storedVariable = new JObject(
                new JProperty("name", variableName),
                new JProperty("type", "Integer"),
                new JProperty("value", variableValue + delta));

            // store the variable by overwriting the exisiting one
            context.UpdateVariable(variableName, storedVariable);
            return new ValueTask<JToken>(storedVariable);
        }
    }
}
