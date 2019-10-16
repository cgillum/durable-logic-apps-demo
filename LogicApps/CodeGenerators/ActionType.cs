using System;
using System.Collections.Generic;
using System.Text;

namespace LogicApps.CodeGenerators
{
    /// <summary>
    /// The different types of actions that are supported by the code generator.
    /// </summary>
    internal enum ActionType
    {
        /// <summary>
        /// Inline actions are implemented directly in the function body.
        /// </summary>
        Inline,

        /// <summary>
        /// Method actions can be generated as direct function calls.
        /// </summary>
        Method,

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
