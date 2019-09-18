using System;
using System.Collections.Generic;
using System.Text;
using LogicApps.Emulation.Actions;
using LogicApps.Schema;

namespace LogicApps.Emulation
{
    internal static class ActionFactory
    {
        // We'll add more to this dictionary as we add more actions
        private static Dictionary<WorkflowActionType, ActionBase> actionMap = new Dictionary<WorkflowActionType, ActionBase>
        {
            { WorkflowActionType.Compose, new ComposeAction() },
            { WorkflowActionType.Http, new HttpAction() },
            { WorkflowActionType.InitializeVariable, new InitializeVariableAction() },
            { WorkflowActionType.IncrementVariable, new IncrementVariableAction() },
        };

        public static ActionBase GetAction(WorkflowActionType type)
        {
            return actionMap[type];
        }
    }
}
