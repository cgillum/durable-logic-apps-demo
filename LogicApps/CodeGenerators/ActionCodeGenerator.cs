namespace LogicApps.CodeGenerators
{
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Base class for all action code generators.
    /// </summary>
    internal abstract class ActionCodeGenerator
    {
        public abstract ActionType ActionType { get; }

        protected abstract IEnumerable<string> OnGenerateStatements(JToken input, ExpressionContext expressionContext);

        public IEnumerable<string> GenerateStatements(string actionName, JToken input, ExpressionContext expressionContext)
        {
            // Generate the statements. This will also populate the expression context with parameter info.
            IEnumerable<string> mainLogicStatements = this.OnGenerateStatements(input, expressionContext);

            // If this is an activity function, then we need to also generate code for reading parameter data
            // from the activity context.
            if (!expressionContext.IsOrchestration)
            {
                List<(string type, string name)> parameters = expressionContext.GetParameters(actionName).ToList();
                if (parameters.Count == 1)
                {
                    yield return $"var {parameters[0].name} = context.GetInput<JToken>();";
                }
                else if (parameters.Count > 1)
                {
                    yield return "var parameters = context.GetInput<JArray>();";
                    for (int i = 0; i < parameters.Count; i++)
                    {
                        yield return $"var {parameters[i].name} = parameters[{i}];";
                    }
                }
            }

            // Finally, return the body statements
            foreach (string statement in mainLogicStatements)
            {
                yield return statement;
            }
        }
    }
}
