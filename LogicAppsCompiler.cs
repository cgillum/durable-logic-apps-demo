using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dynamitey.DynamicObjects;
using LogicApps.LogicApps.CodeGenerators;
using LogicApps.Schema;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Newtonsoft.Json.Linq;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace LogicApps
{
    public static class LogicAppsCompiler
    {
        private static readonly IReadOnlyDictionary<WorkflowActionType, ActionCodeGenerator> ActionCodeGenerators = new Dictionary<WorkflowActionType, ActionCodeGenerator>()
        {
            { WorkflowActionType.Compose, new ComposeCodeGenerator() },
            { WorkflowActionType.Http, new HttpCodeGenerator() },
        };

        public static void Compile(string workflowName, WorkflowDocument doc, TextWriter codeWriter)
        {
            // Resources for learning how to use Roslyn to generate code:
            // https://carlos.mendible.com/2017/03/02/create-a-class-with-net-core-and-roslyn/ (seems to be the most up-to-date)
            // http://roslynquoter.azurewebsites.net/ (use with caution - this creates excessively verbose Roslyn code - most .WithXXX() methods could be simplified to .AddXXX())

            var workspace = new AdhocWorkspace();
            var cu = SF.CompilationUnit().AddUsings(
                SF.UsingDirective(SF.IdentifierName("System")),
                SF.UsingDirective(SF.IdentifierName("System.Collections.Generic")),
                SF.UsingDirective(SF.IdentifierName("Microsoft.Azure.WebJobs")),
                SF.UsingDirective(SF.IdentifierName("Microsoft.Azure.WebJobs.Extensions.DurableTask")),
                SF.UsingDirective(SF.IdentifierName("Newtonsoft.Json.Linq")));

            var ns = SF.NamespaceDeclaration(SF.IdentifierName("LogicAppsDemoApp.GeneratedCode"));

            var @class = SF.ClassDeclaration(workflowName).AddModifiers(SF.Token(SyntaxKind.StaticKeyword));

            // Fields for storing outputs and variables
            @class = @class.AddMembers(CreateStaticDictionary<string, JToken>("Outputs"));
            
            // Trigger functions are declared first
            foreach ((string name, WorkflowTrigger trigger) in doc.Definition.Triggers)
            {
                @class = @class.AddMembers(CreateTriggerFunction(name, trigger, workflowName));
            }

            // Sort the actions based on their dependencies
            IReadOnlyList<(string, WorkflowAction)> sortedActions = TopologicalSort.Sort(
                doc.Definition.Actions.Values,
                a => a.Dependencies.Select(p => p.Key).ToList(),
                a => a.Name).Select(a => (a.Name, a)).ToList().AsReadOnly();

            var orchestrationMethod = CreateFunction("Task", "Orchestrator", workflowName)
                .AddParameterListParameters(
                    CreateBindingParameter(
                        "OrchestrationTrigger",
                        "IDurableOrchestrationContext",
                        "context"))
                .AddBodyStatements(
                    GenerateOrchestratorStatements(sortedActions).ToArray())
                .NormalizeWhitespace();

            // The one orchestrator function comes after the trigger(s)
            @class = @class.AddMembers(orchestrationMethod);

            // All helper functions go next
            @class = @class.AddMembers(GetActionMethods(doc).ToArray());

            ns = ns.AddMembers(@class);
            cu = cu.AddMembers(ns);

            var options = workspace.Options;
            workspace.Options.WithChangedOption(CSharpFormattingOptions.IndentBraces, true);
            SyntaxNode formattedNode = Formatter.Format(cu, workspace, options);
            formattedNode.WriteTo(codeWriter);
        }

        static FieldDeclarationSyntax CreateStaticDictionary<TKey, TValue>(string fieldName)
        {
            string typeName = $"Dictionary<{typeof(TKey).Name}, {typeof(TValue).Name}>";
            VariableDeclarationSyntax variable = SF.VariableDeclaration(SF.ParseTypeName(typeName))
                .AddVariables(SF.VariableDeclarator(fieldName)
                    .WithInitializer(SF.EqualsValueClause(SF.ObjectCreationExpression(SF.ParseTypeName($"{typeName}()")))));

            FieldDeclarationSyntax field = SF.FieldDeclaration(variable).AddModifiers(
                SF.Token(SyntaxKind.PrivateKeyword),
                SF.Token(SyntaxKind.StaticKeyword),
                SF.Token(SyntaxKind.ReadOnlyKeyword));

            return field;
        }

        static IReadOnlyList<ActionCodeGenerator> GetCodeGenerators(IEnumerable<WorkflowAction> actions)
        {
            IReadOnlyList<WorkflowAction> sortedActions = TopologicalSort.Sort(
                actions,
                a => a.Dependencies.Select(p => p.Key).ToList(),
                a => a.Name);

            var sortedCodeGenerators = new List<ActionCodeGenerator>();
            foreach (WorkflowAction actionDefinition in sortedActions)
            {
                sortedCodeGenerators.Add(ActionCodeGenerators[actionDefinition.Type]);
            }

            return sortedCodeGenerators.AsReadOnly();
        }

        static MethodDeclarationSyntax CreateTriggerFunction(string functionName, WorkflowTrigger trigger, string workflowName)
        {
            switch (trigger.Type)
            {
                case WorkflowTriggerType.Recurrence:
                    MethodDeclarationSyntax function = CreateFunction("async Task", $"{functionName}Trigger", functionName)
                        .AddParameterListParameters(
                            CreateBindingParameter(
                                "TimerTrigger",
                                "TimerInfo",
                                "myTimer",
                                $@"""{GetCronExpression(trigger.Recurrence)}"""),
                            CreateBindingParameter(
                                "DurableClient",
                                "IDurableClient",
                                "client"),
                            CreateParameter("ILogger", "log"))
                        .AddBodyStatements(
                            SF.ParseStatement($"string instanceId = await client.StartNewAsync(\"{workflowName}\", null);\n"),
                            SF.ParseStatement(@"log.LogInformation($""Started workflow {functionName}, instance ID = {instanceId}."");")
                        );
                    return function;
                default:
                    throw new ArgumentException($"Trigger type '{trigger.Type}' is not supported.");
            }
        }

        static IEnumerable<StatementSyntax> GenerateOrchestratorStatements(IReadOnlyList<(string, WorkflowAction)> sortedActions)
        {
            foreach ((string name, WorkflowAction action) in sortedActions)
            {
                ActionCodeGenerator generator = ActionCodeGenerators[action.Type];
                switch (generator.ActionType)
                {
                    case ActionType.Inline:
                        yield return SF.ParseStatement($@"Outputs[""{name}""] = {name}(context);");
                        break;
                    case ActionType.Http:
                        yield return SF.ParseStatement($@"Outputs[""{name}""] = await {name}(context);");
                        break;
                    case ActionType.Activity:
                        yield return SF.ParseStatement($@"Outputs[""{name}""] = await context.CallActivityAsync<JToken>(""{name}"", null);");
                        break;
                    default:
                        throw new NotImplementedException($"Action type '{generator.ActionType}' is not supported.");
                }

            }
        }

        static IEnumerable<MethodDeclarationSyntax> GetActionMethods(WorkflowDocument doc)
        {
            foreach ((string name, WorkflowAction action) in doc.Definition.Actions)
            {
                // TODO: Method return types and implementations
                MethodDeclarationSyntax method;
                switch (action.Type)
                {
                    case WorkflowActionType.Compose:
                        method = CreateStaticMethod("JToken", name).AddParameterListParameters(
                            CreateParameter("IDurableOrchestrationContext", "context"));
                        break;
                    default:
                        method = CreateFunction("JToken", name, name)
                            .AddParameterListParameters(
                                CreateBindingParameter("ActivityTrigger", "IDurableActivityContext", "context"));
                        break;
                }

                yield return method.AddBodyStatements(SF.ParseStatement("throw new NotImplementedException();"));
            }
        }

        static MethodDeclarationSyntax CreateFunction(string returnType, string methodName, string functionName)
        {
            // All functions must be public and static.
            MethodDeclarationSyntax method = CreateStaticMethod(returnType, methodName, isPublic: true);

            // All functions must have a [FunctionName("XXX")] attribute
            AttributeSyntax attribute = SF.Attribute(SF.IdentifierName("FunctionName"))
                .AddArgumentListArguments(
                    SF.AttributeArgument(SF.LiteralExpression(SyntaxKind.StringLiteralExpression, SF.Literal(functionName))));

            return method.AddAttributeLists(SF.AttributeList(SF.SingletonSeparatedList(attribute)));
        }

        static MethodDeclarationSyntax CreateStaticMethod(string returnType, string methodName, bool isPublic = false)
        {
            MethodDeclarationSyntax staticMethod = SF.MethodDeclaration(
                SF.IdentifierName(returnType),
                SF.Identifier(methodName));

            if (isPublic)
            {
                staticMethod = staticMethod.AddModifiers(SF.Token(SyntaxKind.PublicKeyword));
            }

            return staticMethod.AddModifiers(SF.Token(SyntaxKind.StaticKeyword));
        }

        static ParameterSyntax CreateBindingParameter(
            string attributeName,
            string parameterType,
            string parameterName,
            string attributeParameterExpression = null)
        {
            var parameter = CreateParameter(parameterType, parameterName);
            var attribute = SF.Attribute(SF.IdentifierName(attributeName));
            if (!string.IsNullOrEmpty(attributeParameterExpression))
            {
                attribute = attribute.WithArgumentList(SF.ParseAttributeArgumentList($"({attributeParameterExpression})"));
            }

            return parameter.AddAttributeLists(SF.AttributeList(SF.SingletonSeparatedList(attribute)));
        }

        static ParameterSyntax CreateParameter(string parameterType, string parameterName)
        {
            return SF.Parameter(SF.Identifier(parameterName)).WithType(SF.IdentifierName(parameterType));
        }

        static MethodDeclarationSyntax CreateComposeMethod(string methodName, JToken input)
        {
            //string inputJson = input.ToString(Formatting.None);

            MethodDeclarationSyntax method = SF.MethodDeclaration(SF.IdentifierName("JToken"), SF.Identifier(methodName))
                .AddModifiers(SF.Token(SyntaxKind.StaticKeyword))
                .AddBodyStatements(SF.ParseStatement("throw new NotImplementedException();").NormalizeWhitespace());

            return method;
        }

        static string GetCronExpression(WorkflowRecurrence recurrence)
        {
            // TODO: This is definitely broken in some cases - very much untested
            switch (recurrence.Frequency.ToLowerInvariant())
            {
                case "second":
                    return $"{recurrence.Interval} * * * * *";
                case "minute":
                    return $"* {recurrence.Interval} * * * *";
                case "hour":
                    return $"* * {recurrence.Interval} * * *";
                case "day":
                    return $"* * * {recurrence.Interval} * *";
                case "month":
                    return $"* * * * {recurrence.Interval} *";
                case "year":
                    return $"* * * * * {recurrence.Interval}";
                default:
                    throw new ArgumentException($"Recurrence frequency '{recurrence.Frequency}' is not supported.");
            }
        }
    }
}
