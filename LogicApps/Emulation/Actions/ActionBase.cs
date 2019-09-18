using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LogicApps.Emulation.Actions
{
    internal abstract class ActionBase
    {
        public abstract ValueTask<JToken> ExecuteAsync(JToken input, WorkflowContext context);
    }
}
