using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Dynamitey.DynamicObjects;
using LogicApps.Schema;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace LogicApps
{
    public static class LogicAppsCompiler
    {
        public static void Compile(string workflowName, WorkflowDocument doc, TextWriter codeWriter)
        {
            // Resources for learning how to use Roslyn to generate code:
            // https://carlos.mendible.com/2017/03/02/create-a-class-with-net-core-and-roslyn/ (seems to be the most up-to-date)
            // http://roslynquoter.azurewebsites.net/ (use with caution - this creates excessively verbose Roslyn code - most .WithXXX() methods could be simplified to .AddXXX())

            var workspace = new AdhocWorkspace();
            var cu = SF.CompilationUnit()
                .AddUsing("System")
                .AddUsing("Microsoft.Azure.WebJobs")
                .AddUsing("Microsoft.Azure.WebJobs.Extensions.DurableTask")
                .AddUsing("Newtonsoft.Json.Linq");

            var ns = SF.NamespaceDeclaration(SF.IdentifierName("LogicAppsDemoApp.GeneratedCode"));

            // Trigger functions are declared first
            var @class = SF.ClassDeclaration(workflowName).AddModifiers(SF.Token(SyntaxKind.StaticKeyword));
            foreach ((string name, WorkflowTrigger trigger) in doc.Definition.Triggers)
            {
                @class = @class.AddMembers(CreateTriggerFunction(name, trigger, workflowName));
            }

            var orchestrationMethod = CreateFunction("Task", "Orchestrator", workflowName)
                .AddParameterListParameters(
                    CreateBindingParameter(
                        "OrchestrationTrigger",
                        "IDurableOrchestrationContext",
                        "context"))
                .AddBodyStatements(
                    SF.ParseStatement(@"throw new NotImplementedException();").NormalizeWhitespace());

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

        static CompilationUnitSyntax AddUsing(this CompilationUnitSyntax cu, string @namespace)
        {
            return cu.AddUsings(SF.UsingDirective(SF.IdentifierName(@namespace)));
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
                    // TODO: Body
                    return function;
                default:
                    throw new ArgumentException($"Trigger type '{trigger.Type}' is not supported.");
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
                        method = CreateStaticMethod("void", name);
                        break;
                    default:
                        method = CreateFunction("void", name, name)
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
