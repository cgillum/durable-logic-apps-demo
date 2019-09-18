using System;
using System.IO;
using System.Text;
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
            ////await Emulator.ExecuteAsync(doc);

            // Generate the function code using the old CodeGen APIs
            var buffer = new StringBuilder(4096);
            using (var writer = new StringWriter(buffer))
            {
                CodeDomCodeGenerator.Generate("ComposeHttp", writer);
            }

            Console.WriteLine(buffer.ToString());
            Console.WriteLine();

            // Do the same thing but using the Roslyn compiler
            buffer.Clear();
            using (var writer = new StringWriter(buffer))
            {
                LogicAppsCompiler.Compile("ComposeHttp", writer);
            }

            Console.WriteLine(buffer.ToString());

            Console.WriteLine();
            Console.WriteLine("Done");
            await Task.Yield();
        }
    }
}
