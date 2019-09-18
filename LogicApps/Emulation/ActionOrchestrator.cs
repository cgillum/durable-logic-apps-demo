using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using LogicApps.Schema;
using Newtonsoft.Json.Linq;

namespace LogicApps.Emulation
{
    internal class ActionOrchestrator
    {
        public static ValueTask<JToken> ExecuteAsync(WorkflowAction workflowAction, WorkflowContext context)
        {
            var action = ActionFactory.GetAction(workflowAction.Type);
            return action.ExecuteAsync(workflowAction.Inputs, context);
        }
    }
}
