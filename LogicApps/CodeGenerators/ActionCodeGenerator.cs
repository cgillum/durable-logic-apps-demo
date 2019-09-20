using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace LogicApps.LogicApps.CodeGenerators
{
    /// <summary>
    /// Base class for all action code generators.
    /// </summary>
    internal abstract class ActionCodeGenerator
    {
        public abstract ActionType ActionType { get; }

        public abstract IEnumerable<string> GenerateStatements(JToken inputs);
    }
}
