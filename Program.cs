using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using LogicApps.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            (string fileName, string workflowName) = ("05.queue-binding.json", "QueueBindings");
            //(string fileName, string workflowName) = ("06.event-hub-binding.json", "EventHubBindings");

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

            ProjectArtifacts artifacts = WriteSourceCode(projectDir, workflowName, doc);
            WriteProjectFile(projectDir, workflowName, artifacts);
            WriteHostJsonFile(projectDir, workflowName);
            WriteLocalSettingsJsonFile(projectDir, artifacts);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Successfully created functions project at " + projectDir);
            Console.ResetColor();
        }

        static ProjectArtifacts WriteSourceCode(string projectDirectory, string workflowName, WorkflowDocument doc)
        {
            string outputFilePath = Path.Combine(projectDirectory, workflowName + ".cs");
            Console.WriteLine($"Generating {outputFilePath}...");

            using (var writer = File.CreateText(outputFilePath))
            {
                return LogicAppsCompiler.Compile(workflowName, doc, writer);
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

        static void WriteLocalSettingsJsonFile(string projectDirectory, ProjectArtifacts artifacts)
        {
            string fileName = "local.settings.json";
            string outputFilePath = Path.Combine(projectDirectory, fileName);
            Console.WriteLine($"Generating {outputFilePath}...");

            string localSettingsText = LoadResourceText(fileName);
            localSettingsText = localSettingsText.Replace("%StorageConnectionString%", "UseDevelopmentStorage=true");

            JObject settings = JObject.Parse(localSettingsText);
            JObject appSettings = (JObject)settings["Values"];
            
            foreach (string appSettingName in artifacts.AppSettings)
            {
                if (!appSettings.TryGetValue(appSettingName, out _))
                {
                    appSettings[appSettingName] = "[Placeholder]";
                }
            }

            using (StreamWriter writer = File.CreateText(outputFilePath))
            {
                writer.Write(settings.ToString(Formatting.Indented));
            }
        }

        static void WriteProjectFile(string projectDirectory, string workflowName, ProjectArtifacts artifacts)
        {
            string outputFilePath = Path.Combine(projectDirectory, $"Durable{workflowName}.csproj");
            Console.WriteLine($"Generating {outputFilePath}...");

            string fileName = "ProjectFile.xml";
            string projectFileText = LoadResourceText(fileName);

            XElement xml = XElement.Parse(projectFileText);
            XElement packageReferencesXml = xml.Element("ItemGroup");

            foreach ((string package, string version) in artifacts.Extensions)
            {
                packageReferencesXml.Add(
                    new XElement("PackageReference",
                        new XAttribute("Include", package),
                        new XAttribute("Version", version)));
            }

            using (StreamWriter writer = File.CreateText(Path.Combine(projectDirectory, outputFilePath)))
            {
                writer.Write(xml);
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
