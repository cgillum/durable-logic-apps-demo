using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicApps.Schema;
using Newtonsoft.Json;

namespace LogicApps.TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // These JSON files are the logic apps that we're testing
            //(string fileName, string workflowName) = ("01.simple-http.json", "ComposeHttp");
            //(string fileName, string workflowName) = ("03.foreach.json", "ForEach");
            //(string fileName, string workflowName) = ("04.teams-connection.json", "TeamsConnection");
            (string fileName, string workflowName) = ("05.event-hub.json", "EventHubLogicApp");

            string sample1FilePath = Path.Join(Environment.CurrentDirectory, "Samples", fileName);
            Console.WriteLine($"Loading Logic App '{workflowName}' workflow definition from {sample1FilePath}...");

            string sample1JsonText = File.ReadAllText(sample1FilePath);
            WorkflowDocument doc = JsonConvert.DeserializeObject<WorkflowDocument>(sample1JsonText);

            if (args.Any(arg => arg == "--proj"))
            {
                bool force = args.Any(arg => arg == "--force");
                GenerateFunctionProject(workflowName, doc, force);
            }
            else
            {
                LogicAppsCompiler.Compile(workflowName, doc, Console.Out);
            }

            Console.WriteLine();
            Console.WriteLine("Done");
        }

        static void GenerateFunctionProject(string workflowName, WorkflowDocument doc, bool force)
        {
            string projectDir = Path.Combine(Environment.CurrentDirectory, workflowName);
            if (Directory.Exists(projectDir))
            {
                if (force)
                {
                    Directory.Delete(projectDir, recursive: true);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"There is already a directory at {projectDir}. Use --force to override.");
                    Console.ResetColor();
                    Environment.Exit(2);
                    return;
                }
            }

            Directory.CreateDirectory(projectDir);

            WriteSourceCode(projectDir, workflowName, doc);
            WriteProjectFile(projectDir, workflowName);
            WriteHostJsonFile(projectDir, workflowName);
            WriteLocalSettingsJsonFile(projectDir);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Successfully created functions project at " + projectDir);
            Console.ResetColor();
        }

        static void WriteSourceCode(string projectDirectory, string workflowName, WorkflowDocument doc)
        {
            string outputFilePath = Path.Combine(projectDirectory, workflowName + ".cs");
            Console.WriteLine($"Generating {outputFilePath}...");

            using (var writer = File.CreateText(outputFilePath))
            {
                LogicAppsCompiler.Compile(workflowName, doc, writer);
            }
        }

        static void WriteHostJsonFile(string projectDirectory, string workflowName)
        {
            string fileName = "host.json";
            string outputFilePath = Path.Combine(projectDirectory, fileName);
            Console.WriteLine($"Generating {outputFilePath}...");

            string hostJsonText = LoadResourceText(fileName);
            hostJsonText = hostJsonText.Replace("%TaskHubName%", $"{workflowName}TaskHub");

            using (StreamWriter writer = File.CreateText(outputFilePath))
            {
                writer.Write(hostJsonText);
            }
        }

        static void WriteLocalSettingsJsonFile(string projectDirectory)
        {
            string fileName = "local.settings.json";
            string outputFilePath = Path.Combine(projectDirectory, fileName);
            Console.WriteLine($"Generating {outputFilePath}...");

            string localSettingsText = LoadResourceText(fileName);
            localSettingsText = localSettingsText.Replace("%StorageConnectionString%", "UseDevelopmentStorage=true");

            using (StreamWriter writer = File.CreateText(outputFilePath))
            {
                writer.Write(localSettingsText);
            }
        }

        static void WriteProjectFile(string projectDirectory, string workflowName)
        {
            string outputFilePath = Path.Combine(projectDirectory, $"Durable{workflowName}.csproj");
            Console.WriteLine($"Generating {outputFilePath}...");

            string fileName = "ProjectFile.xml";
            string projectFileText = LoadResourceText(fileName);
            using (StreamWriter writer = File.CreateText(Path.Combine(projectDirectory, outputFilePath)))
            {
                writer.Write(projectFileText);
            }
        }

        static string LoadResourceText(string fileName)
        {
            // The format is {ProjectDefaultNamespace}.{FolderName}.{FileName}
            string resourceName = $"LogicApps.Templates." + fileName;
            Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (resourceStream == null)
            {
                throw new ArgumentException($"The specified resource '{fileName}' was not found in the current assembly.");
            }

            using (StreamReader reader = new StreamReader(resourceStream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
