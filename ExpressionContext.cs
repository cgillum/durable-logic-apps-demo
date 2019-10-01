using System.Collections.Generic;

namespace LogicApps
{
    public class ExpressionContext
    {
        private readonly Dictionary<string, List<(string type, string name)>> parameters;

        public ExpressionContext()
        {
            this.parameters = new Dictionary<string, List<(string type, string name)>>();
        }

        public bool IsOrchestration { get; set; }

        public string CurrentActionName { get; set; }

        public bool IsTriggerBodyReferenced { get; set; }

        public string AddParameter(string type, string name)
        {
            name = Utils.SanitizeName(name);

            List<(string type, string name)> paramList;
            if (!this.parameters.TryGetValue(this.CurrentActionName, out paramList))
            {
                paramList = new List<(string type, string name)>();
                this.parameters.Add(this.CurrentActionName, paramList);
            }

            paramList.Add((type, name));
            return name;
        }

        internal IEnumerable<(string type, string name)> GetParameters(string actionName)
        {
            if (this.parameters.TryGetValue(actionName, out List<(string type, string name)> paramList))
            {
                foreach (var pair in paramList)
                {
                    yield return pair;
                }
            }
        }
    }
}
