using System;
using System.IO;
using System.Threading.Tasks;
using LogicApps.Schema;
using Newtonsoft.Json;

namespace LogicApps.TestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // These JSON files are the logic apps that we're testing
            string fileName = "01.simple-http.json";
            //string fileName = "03.foreach.json";

            string sample1FilePath = Path.Join(Environment.CurrentDirectory, "Samples", fileName);
            Console.WriteLine($"Loading Logic App workflow definition from {sample1FilePath}...");

            string sample1JsonText = File.ReadAllText(sample1FilePath);
            WorkflowDocument doc = JsonConvert.DeserializeObject<WorkflowDocument>(sample1JsonText);

            // Run the workflow in the Logic Apps emulator
            await Emulator.ExecuteAsync(doc);

            Console.WriteLine();
            Console.WriteLine("Done");
        }
    }
}
