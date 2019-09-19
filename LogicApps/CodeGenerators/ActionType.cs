using System;
using System.Collections.Generic;
using System.Text;

namespace LogicApps.LogicApps.CodeGenerators
{
    /// <summary>
    /// The different types of actions that are supported by the code generator.
    /// </summary>
    internal enum ActionType
    {
        /// <summary>
        /// Inline actions can be generated as direct function calls.
        /// </summary>
        Inline,

        /// <summary>
        /// The action must be wrapped in a durable activity function.
        /// </summary>
        Activity,

        /// <summary>
        /// The action is implemented as a durable HTTP call.
        /// </summary>
        Http,
    }
}
