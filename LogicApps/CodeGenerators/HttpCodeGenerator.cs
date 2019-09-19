using System.Collections.Generic;

namespace LogicApps.LogicApps.CodeGenerators
{
    class HttpCodeGenerator : ActionCodeGenerator
    {
        // TODO: Change this to ActivityType.Inline and use the Durable HTTP feature
        public override ActionType ActionType => ActionType.Activity;

        public override IEnumerable<string> GenerateStatements()
        {
            yield return "throw new NotImplementedException();";
        }
    }
}
