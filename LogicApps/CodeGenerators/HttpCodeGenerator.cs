namespace LogicApps.LogicApps.CodeGenerators
{
    using Newtonsoft.Json.Linq;
    using System.Collections.Generic;

    class HttpCodeGenerator : ActionCodeGenerator
    {
        // TODO: Change this to ActivityType.Inline and use the Durable HTTP feature
        public override ActionType ActionType => ActionType.Activity;

        public override IEnumerable<string> GenerateStatements(JToken inputs)
        {
            yield return "throw new NotImplementedException();";
        }
    }
}
