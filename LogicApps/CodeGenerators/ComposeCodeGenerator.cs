using System.Collections.Generic;

namespace LogicApps.LogicApps.CodeGenerators
{
    class ComposeCodeGenerator : ActionCodeGenerator
    {
        public override ActionType ActionType => ActionType.Inline;

        public override IEnumerable<string> GenerateStatements()
        {
            yield return "throw new NotImplementedException();";
        }
    }
}
