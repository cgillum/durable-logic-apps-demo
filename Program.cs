using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LogicAppsTesting.Actions;
using LogicAppsTesting.Schema;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogicAppsTesting
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // These JSON files are the logic apps that we're testing
            string fileName = "01.simple-http.json";
            //string fileName = "03.foreach.json";
            string sample1FilePath = Path.Join(Environment.CurrentDirectory, "Samples", fileName);
            Console.WriteLine(sample1FilePath);

            string sample1JsonText = File.ReadAllText(sample1FilePath);

            // Deserialize the workflow JSON into objects, and then sort those objects into the correct execution order
            WorkflowDocument doc = JsonConvert.DeserializeObject<WorkflowDocument>(sample1JsonText);
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

            // We'll add more to this dictionary as we add more actions
            var actionMap = new Dictionary<WorkflowActionType, ActionBase>
            {
                { WorkflowActionType.Compose, new ComposeAction() },
                { WorkflowActionType.Http, new HttpAction() },
            };

            // Mock the Durable Orchestration pieces. Eventually, we'll use the real context instead of this fake one.
            var durableContext = new Mock<IDurableOrchestrationContext>(MockBehavior.Strict);
            durableContext
                .Setup(x => x.CallHttpAsync(It.IsAny<DurableHttpRequest>()))
                .ReturnsAsync(() => new DurableHttpResponse(System.Net.HttpStatusCode.OK));

            var context = new WorkflowContext(durableContext.Object);

            // Execute the logic app!
            foreach (WorkflowAction action in sortedActions)
            {
                JToken result = await actionMap[action.Type].ExecuteAsync(action.Inputs, context);

                // Outputs are saved to the context, so that expressions can reference them in later steps
                context.SaveOutput(action.Name, result);

                Console.WriteLine($"Executed {action.Name}. Result = {result}");
            }

            Console.WriteLine();
            Console.WriteLine("Done");
        }
    }
}
