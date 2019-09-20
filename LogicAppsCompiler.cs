using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                SF.UsingDirective(SF.IdentifierName("System.Net.Http")),
                SF.UsingDirective(SF.IdentifierName("System.Threading.Tasks")),
                SF.UsingDirective(SF.IdentifierName("Microsoft.Azure.WebJobs")),
                SF.UsingDirective(SF.IdentifierName("Microsoft.Azure.WebJobs.Extensions.DurableTask")),
                SF.UsingDirective(SF.IdentifierName("Microsoft.Extensions.Logging")),
                SF.UsingDirective(SF.IdentifierName("Microsoft.Extensions.Primitives")),
                SF.UsingDirective(SF.IdentifierName("Newtonsoft.Json")),
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

            var orchestrationMethod = CreateFunction("async Task", "Orchestrator", workflowName)
                .AddParameterListParameters(
                    CreateBindingParameter(
                        "OrchestrationTrigger",
                        "IDurableOrchestrationContext",
                        "context"))
                .WithBody(SF.Block())
                .AddBodyStatements(
                    GenerateOrchestratorStatements(sortedActions).ToArray());

            // The one orchestrator function comes after the trigger(s)
            @class = @class.AddMembers(orchestrationMethod);

            // All helper functions go next
            @class = @class.AddMembers(GetActionMethods(sortedActions).ToArray());

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
                            SF.ParseStatement($@"log.LogInformation($""Started workflow '{functionName}', instance ID = {{instanceId}}."");")
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
                string resultVariable = $"resultOf{action.Name}";

                StatementSyntax statement;
                switch (generator.ActionType)
                {
                    // CONSIDER: Create intermediate variables for easier debugging
                    case ActionType.Inline:
                        statement = SF.ParseStatement($@"JToken {resultVariable} = {name}(context);");
                        break;
                    case ActionType.Http:
                        statement = SF.ParseStatement($@"JToken {resultVariable} = await {name}(context);");
                        break;
                    case ActionType.Activity:
                        // TODO: If an activity needs access to variables, outputs, or parameters, then we would need to pass them 
                        //       as a parameter to the activity function. Not yet clear if we need activity functions at all, though.
                        statement = SF.ParseStatement($@"JToken {resultVariable} = await context.CallActivityAsync<JToken>(""{name}"", null);");
                        break;
                    default:
                        throw new NotImplementedException($"Action type '{generator.ActionType}' is not supported.");
                }

                // Add extra whitespace to make the generated code easier to read
                // TODO: Need to make sure the whole file is consistent in terms of using \r\n or \n for newlines
                yield return statement.WithTrailingTrivia(SF.CarriageReturnLineFeed);
                yield return SF.ParseStatement($@"Outputs[""{name}""] = {resultVariable};")
                    .WithTrailingTrivia(SF.CarriageReturnLineFeed, SF.CarriageReturnLineFeed);
            }
        }

        static IEnumerable<MethodDeclarationSyntax> GetActionMethods(IReadOnlyList<(string, WorkflowAction)> sortedActions)
        {
            foreach ((string name, WorkflowAction action) in sortedActions)
            {
                ActionCodeGenerator generator = ActionCodeGenerators[action.Type];

                MethodDeclarationSyntax method;
                switch (generator.ActionType)
                {
                    case ActionType.Inline:
                        method = CreateStaticMethod("JToken", name)
                                    .AddParameterListParameters(
                                        CreateParameter("IDurableOrchestrationContext", "context"));
                        break;
                    case ActionType.Http:
                        method = CreateStaticMethod("async Task<JToken>", name)
                                    .AddParameterListParameters(
                                        CreateParameter("IDurableOrchestrationContext", "context"));
                        break;
                    case ActionType.Activity:
                        method = CreateFunction("JToken", name, name)
                                    .AddParameterListParameters(
                                        CreateBindingParameter("ActivityTrigger", "IDurableActivityContext", "context"));
                        break;
                    default:
                        throw new NotImplementedException($"Action type '{generator.ActionType}' is not supported.");
                }

                // The generators will dynamically produce code that we inject into the method body.
                yield return method.AddBodyStatements(
                    generator.GenerateStatements(action.Inputs)
                        .Select(codeLine => SF.ParseStatement(codeLine).WithTrailingTrivia(SF.CarriageReturnLineFeed)).ToArray());
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

            return staticMethod.AddModifiers(
                SF.Token(isPublic ? SyntaxKind.PublicKeyword : SyntaxKind.PrivateKeyword),
                SF.Token(SyntaxKind.StaticKeyword));
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
