namespace LogicApps.LogicApps.CodeGenerators
{
    using System.Collections.Generic;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Base class for all action code generators.
    /// </summary>
    internal abstract class ActionCodeGenerator
    {
        public abstract ActionType ActionType { get; }

        public abstract IEnumerable<string> GenerateStatements(JToken input);
    }
}
