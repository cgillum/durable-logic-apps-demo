namespace LogicAppsTesting.Actions
{
    using Newtonsoft.Json.Linq;
    using System.Threading.Tasks;

    public class InitializeVariableAction : ActionBase
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

    public class IncrementVariableAction : ActionBase
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
