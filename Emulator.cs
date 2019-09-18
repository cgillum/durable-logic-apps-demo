using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LogicApps.Emulation;
using LogicApps.Schema;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Moq;
using Newtonsoft.Json.Linq;

namespace LogicApps.TestApp
{
    static class Emulator
    {
        public static async Task ExecuteAsync(WorkflowDocument doc)
        {
            IReadOnlyList<WorkflowAction> sortedActions = TopologicalSort.Sort(
                doc.Definition.Actions.Values,
                a => a.Dependencies.Select(p => p.Key).ToList(),
                a => a.Name);

            // Print out the execution order to make sure it looks correct
            Console.WriteLine("Workflow dependency tree:");
            Console.WriteLine("=========================");
            foreach (WorkflowAction action in sortedActions)
            {
                Console.WriteLine($"{action.Name} -> {string.Join(", ", action.Dependencies.Select(pair => $"{pair.Key}: {string.Join(", ", pair.Value)}"))}");
            }

            Console.WriteLine();

            // Mock the Durable Orchestration pieces.
            var durableContext = new Mock<IDurableOrchestrationContext>(MockBehavior.Strict);
            durableContext
                .Setup(x => x.CallHttpAsync(It.IsAny<DurableHttpRequest>()))
                .ReturnsAsync(() => new DurableHttpResponse(System.Net.HttpStatusCode.OK));

            var context = new WorkflowContext(durableContext.Object);

            // Execute the logic app!
            foreach (WorkflowAction action in sortedActions)
            {
                JToken result = await ActionOrchestrator.ExecuteAsync(action, context);

                // Outputs are saved to the context, so that expressions can reference them in later steps
                context.SaveOutput(action.Name, result);

                Console.WriteLine($"Executed {action.Name}. Result = {result}");
            }
        }
    }
}
