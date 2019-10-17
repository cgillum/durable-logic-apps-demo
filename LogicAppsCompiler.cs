using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicApps.CodeGenerators;
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
            { WorkflowActionType.ApiConnection, new ApiConnectionCodeGenerator() },
            { WorkflowActionType.Binding, new BindingCodeGenerator() },
            { WorkflowActionType.Compose, new ComposeCodeGenerator() },
            { WorkflowActionType.Http, new HttpCodeGenerator() },
            { WorkflowActionType.InitializeVariable, new InitializeVariableCodeGenerator() },
            { WorkflowActionType.IncrementVariable, new IncrementVariableCodeGenerator() },
        };

        public static ProjectArtifacts Compile(string workflowName, WorkflowDocument doc, TextWriter codeWriter)
        {
            // Resources for learning how to use Roslyn to generate code:
            // https://carlos.mendible.com/2017/03/02/create-a-class-with-net-core-and-roslyn/ (seems to be the most up-to-date)
            // http://roslynquoter.azurewebsites.net/ (use with caution - this creates excessively verbose Roslyn code - most .WithXXX() methods could be simplified to .AddXXX())

            var workspace = new AdhocWorkspace();
            var cu = SF.CompilationUnit().AddUsings(
                SF.UsingDirective(SF.IdentifierName("System")),
                SF.UsingDirective(SF.IdentifierName("System.Collections.Generic")),
                SF.UsingDirective(SF.IdentifierName("System.Net.Http")),
                SF.UsingDirective(SF.IdentifierName("System.Text")),
                SF.UsingDirective(SF.IdentifierName("System.Threading.Tasks")),
                SF.UsingDirective(SF.IdentifierName("Microsoft.Azure.WebJobs")),
                SF.UsingDirective(SF.IdentifierName("Microsoft.Azure.WebJobs.Extensions.DurableTask")),
                SF.UsingDirective(SF.IdentifierName("Microsoft.Azure.WebJobs.Extensions.Http")),
                SF.UsingDirective(SF.IdentifierName("Microsoft.Extensions.Logging")),
                SF.UsingDirective(SF.IdentifierName("Microsoft.Extensions.Primitives")),
                SF.UsingDirective(SF.IdentifierName("Newtonsoft.Json")),
                SF.UsingDirective(SF.IdentifierName("Newtonsoft.Json.Linq")));

            var ns = SF.NamespaceDeclaration(SF.IdentifierName("LogicAppsDemoApp.GeneratedCode"));

            var @class = SF.ClassDeclaration(workflowName).AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.StaticKeyword));

            // Keep track of any build artifacts that are required (e.g. app settings)
            var artifacts = new ProjectArtifacts();

            // Trigger functions are declared first
            foreach ((string name, WorkflowTrigger trigger) in doc.Definition.Triggers)
            {
                @class = @class.AddMembers(CreateTriggerFunction(name, trigger, workflowName, artifacts));
            }

            // Sort the actions based on their dependencies
            IReadOnlyList<(string, WorkflowAction)> sortedActions = TopologicalSort.Sort(
                doc.Definition.Actions.Values,
                a => a.Dependencies.Select(p => p.Key).ToList(),
                a => a.Name).Select(a => (a.Name, a)).ToList().AsReadOnly();

            var orchestratorStatements = new List<StatementSyntax>();
            var expressionContext = new ExpressionContext();

            // insert connection token if specified in workflow.json 
            if (doc.ConnectionToken != null)
            {
                orchestratorStatements.Add(
                    SF.ParseStatement($"JToken {Utils.GetWorkflowParameterVariableName("connections")} = JToken.Parse({ExpressionCompiler.ConvertJTokenToStringInterpolation(doc.ConnectionToken["value"], expressionContext)});")
                        .WithTrailingTrivia(SF.CarriageReturnLineFeed, SF.CarriageReturnLineFeed));
            }

            var parameterLists = new Dictionary<string, string[]>();
            MethodDeclarationSyntax[] actionMethods = GetActionMethods(sortedActions, expressionContext, artifacts).ToArray();

            orchestratorStatements.AddRange(GenerateOrchestratorStatements(sortedActions, expressionContext));

            var orchestrationMethod = CreateFunction("async Task", "Orchestrator", workflowName)
                .AddParameterListParameters(
                    CreateBindingParameter(
                        "OrchestrationTrigger",
                        "IDurableOrchestrationContext",
                        "context"))
                .WithBody(SF.Block())
                .AddBodyStatements(orchestratorStatements.ToArray());
       
            // The one orchestrator function comes after the trigger(s)
            @class = @class.AddMembers(orchestrationMethod);

            // All action functions go next
            @class = @class.AddMembers(actionMethods);

            // All built-in expression language functions go next
            @class = @class.AddMembers(GetBuildInMethods().ToArray());

            ns = ns.AddMembers(@class);
            cu = cu.AddMembers(ns);

            var options = workspace.Options;
            workspace.Options.WithChangedOption(CSharpFormattingOptions.IndentBraces, true);
            SyntaxNode formattedNode = Formatter.Format(cu, workspace, options);
            formattedNode.WriteTo(codeWriter);

            return artifacts;
        }

        static MethodDeclarationSyntax CreateTriggerFunction(
            string functionName,
            WorkflowTrigger trigger,
            string workflowName,
            ProjectArtifacts artifacts)
        {
            MethodDeclarationSyntax function;
            switch (trigger.Type)
            {
                case WorkflowActionType.Recurrence:
                    function = CreateFunction("async Task", $"{functionName}Trigger", functionName)
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
                    break;
                case WorkflowActionType.Binding:
                    function = CreateFunction("async Task", $"{functionName}Trigger", functionName)
                        .AddParameterListParameters(
                            GetTriggerBindingParameters(trigger, artifacts),
                            CreateBindingParameter(
                                "DurableClient",
                                "IDurableClient",
                                "client"),
                            CreateParameter("ILogger", "log"))
                        .AddBodyStatements(
                            SF.ParseStatement($"string instanceId = await client.StartNewAsync(\"{workflowName}\", input);\n"),
                            SF.ParseStatement($@"log.LogInformation($""Started workflow '{functionName}', instance ID = {{instanceId}}."");")
                        );
                    break;
                case WorkflowActionType.Request:
                    if (trigger.Kind != WorkflowActionKind.Http)
                    {
                        throw new NotSupportedException($"{trigger.Kind} is not a supported request type.");
                    }

                    function = CreateFunction("async Task", $"{functionName}Trigger", functionName)
                        .AddParameterListParameters(
                            CreateBindingParameter(
                                "HttpTrigger",
                                "dynamic", // Can't use JToken directly, so we use `dynamic`. Considered `object` but that can't be cast to JToken.
                                "input",
                                "AuthorizationLevel.Anonymous, \"POST\""),
                            CreateBindingParameter(
                                "DurableClient",
                                "IDurableClient",
                                "client"),
                            CreateParameter("ILogger", "log"))
                        .AddBodyStatements(
                            SF.ParseStatement($"string instanceId = await client.StartNewAsync(\"{workflowName}\", (object)input);\n"),
                            SF.ParseStatement($@"log.LogInformation($""Started workflow '{functionName}', instance ID = {{instanceId}}."");")
                        );
                    break;
                default:
                    throw new ArgumentException($"Trigger type '{trigger.Type}' is not supported.");
            }

            return function;
        }

        static ParameterSyntax GetTriggerBindingParameters(WorkflowTrigger trigger, ProjectArtifacts artifacts)
        {
            JObject inputs = trigger.Inputs;
            string triggerType = inputs["type"].Value<string>();

            string attributeName;
            string attributeParameters;
            string parameterType = "string";

            switch (triggerType)
            {
                case "queueTrigger":
                    attributeName = "QueueTrigger";
                    attributeParameters = $@"""{inputs["queueName"]}"", Connection = ""{inputs["connection"]}""";
                    artifacts.Extensions["Microsoft.Azure.WebJobs.Extensions.Storage"] = "3.0.*";
                    break;
                case "eventHubTrigger":
                    attributeName = "EventHubTrigger";
                    attributeParameters = $@"""{inputs["eventHubName"]}"", Connection = ""{inputs["connection"]}""";
                    artifacts.Extensions["Microsoft.Azure.WebJobs.Extensions.EventHubs"] = "3.0.*";
                    break;
                case "blobTrigger":        // TODO
                case "serviceBusTrigger":  // TODO
                case "cosmosDBTrigger":    // TODO
                default:
                    throw new NotSupportedException($"Binding trigger type '{triggerType}' is not supported.");
            }

            // TODO: Check for other known app settings
            if (inputs.TryGetValue("connection", out JToken appSettingName))
            {
                artifacts.AppSettings.Add((string)appSettingName);
            }

            return CreateBindingParameter(attributeName, parameterType, "input", attributeParameters);
        }

        static ParameterSyntax GetOutputBindingParameters(WorkflowAction action, ProjectArtifacts artifacts)
        {
            JObject inputs = (JObject) action.Inputs;
            string bindingType = inputs["type"].Value<string>();
            string parameterName = inputs["name"].Value<string>();

            string attributeName;
            string attributeParameters;
            string parameterType = "string";

            switch (bindingType)
            {
                case "queue":
                    attributeName = "Queue";
                    attributeParameters = $@"""{inputs["queueName"]}"", Connection = ""{inputs["connection"]}""";
                    artifacts.Extensions["Microsoft.Azure.WebJobs.Extensions.Storage"] = "3.0.8";
                    break;
                case "eventHub":
                    attributeName = "EventHub";
                    attributeParameters = $@"""{inputs["eventHubName"]}"", Connection = ""{inputs["connection"]}""";
                    artifacts.Extensions["Microsoft.Azure.WebJobs.Extensions.EventHubs"] = "3.0.3";
                    break;
                case "blob":        // TODO
                case "serviceBus":  // TODO
                case "cosmosDB":    // TODO
                default:
                    throw new NotSupportedException($"Binding trigger type '{bindingType}' is not supported.");
            }

            // TODO: Check for other known app settings
            if (inputs.TryGetValue("connection", out JToken appSettingName))
            {
                artifacts.AppSettings.Add((string)appSettingName);
            }

            return CreateBindingParameter(attributeName, parameterType, parameterName, attributeParameters);
        }

        static IEnumerable<StatementSyntax> GenerateOrchestratorStatements(IReadOnlyList<(string, WorkflowAction)> sortedActions, ExpressionContext context)
        {
            if (context.IsTriggerBodyReferenced)
            {
                yield return SF.ParseStatement("JToken triggerBody = context.GetInput<JToken>();").WithTrailingTrivia(SF.CarriageReturnLineFeed);
            }

            foreach ((string name, WorkflowAction action) in sortedActions)
            {
                string sanitizedName = Utils.SanitizeName(name);

                ActionCodeGenerator generator = ActionCodeGenerators[action.Type];
                string resultVariable = $"resultOf{sanitizedName}";

                var parameterList = new LinkedList<string>(context.GetParameters(name).Select(p => p.name));
                string paramListString;

                StatementSyntax statement;
                switch (generator.ActionType)
                {
                    case ActionType.Inline:
                        // Inline actions are actions with one or more statements in the main function body
                        foreach (string inlineStatement in generator.GenerateStatements(action.Name, action.Inputs, context))
                        {
                            yield return SF.ParseStatement(inlineStatement).WithTrailingTrivia(SF.CarriageReturnLineFeed);
                        }
                        continue;
                    case ActionType.Method:
                        parameterList.AddFirst("context");
                        paramListString = string.Join(", ", parameterList);
                        statement = SF.ParseStatement($@"JToken {resultVariable} = {sanitizedName}({paramListString});");
                        break;
                    case ActionType.Http:
                        parameterList.AddFirst("context");
                        paramListString = string.Join(", ", parameterList);
                        statement = SF.ParseStatement($@"JToken {resultVariable} = await {sanitizedName}({paramListString});");
                        break;
                    case ActionType.Activity:
                        if (parameterList.Count == 0)
                        {
                            paramListString = "null";
                        }
                        else if (parameterList.Count == 1)
                        {
                            paramListString = parameterList.Single();
                        }
                        else
                        {
                            paramListString = "new [] { " + string.Join(", ", parameterList) + " }";
                        }

                        statement = SF.ParseStatement($@"JToken {resultVariable} = await context.CallActivityAsync<JToken>(""{sanitizedName}"", {paramListString});");
                        break;
                    default:
                        throw new NotImplementedException($"Action type '{generator.ActionType}' is not supported.");
                }

                // Add extra whitespace to make the generated code easier to read
                // TODO: Need to make sure the whole file is consistent in terms of using \r\n or \n for newlines
                yield return statement.WithTrailingTrivia(SF.CarriageReturnLineFeed);
            }
        }

        static IEnumerable<MethodDeclarationSyntax> GetActionMethods(
            IReadOnlyList<(string, WorkflowAction)> sortedActions,
            ExpressionContext expressionContext,
            ProjectArtifacts artifacts)
        {
            foreach ((string actionName, WorkflowAction action) in sortedActions)
            {
                ActionCodeGenerator generator;
                if (!ActionCodeGenerators.TryGetValue(action.Type, out generator))
                {
                    throw new NotSupportedException($"Don't know how to generate code for '{action.Type}' actions.");
                }

                string sanitizedName = Utils.SanitizeName(actionName);
                expressionContext.CurrentActionName = actionName;

                bool hasOutputBinding = false;
                MethodDeclarationSyntax method;
                switch (generator.ActionType)
                {
                    case ActionType.Inline:
                        // Nothing to generate for inline code
                        continue;
                    case ActionType.Method:
                        method = CreateStaticMethod("JToken", sanitizedName)
                                    .AddAttributeLists(
                                        SF.AttributeList(SF.SingletonSeparatedList(SF.Attribute(SF.IdentifierName("Deterministic")))))
                                    .AddParameterListParameters(
                                        CreateParameter("IDurableOrchestrationContext", "context"));
                        expressionContext.IsOrchestration = true;
                        break;
                    case ActionType.Http:
                        method = CreateStaticMethod("async Task<JToken>", sanitizedName)
                                    .AddAttributeLists(
                                        SF.AttributeList(SF.SingletonSeparatedList(SF.Attribute(SF.IdentifierName("Deterministic")))))
                                    .AddParameterListParameters(
                                        CreateParameter("IDurableOrchestrationContext", "context"));
                        expressionContext.IsOrchestration = true;
                        break;
                    case ActionType.Activity:
                        method = CreateFunction("JToken", sanitizedName, sanitizedName)
                                    .AddParameterListParameters(
                                        CreateBindingParameter("ActivityTrigger", "IDurableActivityContext", "context"));
                        expressionContext.IsOrchestration = false;

                        // add output binding parameter for binding action
                        hasOutputBinding = action.Type == WorkflowActionType.Binding;

                        break;
                    default:
                        throw new NotImplementedException($"Action type '{generator.ActionType}' is not supported.");
                }

                // The generators will dynamically produce code that we inject into the method body.
                // This will also populate the expression context with any parameter information.
                method = method.AddBodyStatements(
                    generator.GenerateStatements(actionName, action.Inputs, expressionContext)
                        .Select(codeLine => SF.ParseStatement(codeLine).WithTrailingTrivia(SF.CarriageReturnLineFeed)).ToArray());

                // add input parameters for methods called directly by the orchestrator function
                if (expressionContext.IsOrchestration)
                {
                    foreach ((string inputType, string inputName) in expressionContext.GetParameters(actionName))
                    {
                        var parameter = CreateParameter(inputType, inputName);
                        method = method.AddParameterListParameters(parameter);
                    }
                }

                // out output parameters
                if (hasOutputBinding)
                {
                    var parameter = GetOutputBindingParameters(action, artifacts);
                    method = method.AddParameterListParameters(parameter.AddModifiers(SF.Token(SyntaxKind.OutKeyword)));
                }

                yield return method;
            }
        }

        static IEnumerable<MethodDeclarationSyntax> GetBuildInMethods()
        {
            foreach (var buildInFunction in ExpressionCompiler.BuildInFunctionTypeMap)
            {
                string expression = ExpressionCompiler.BuildInFunctionExpressionMap[buildInFunction.Key];
                MethodDeclarationSyntax method = CreateStaticMethod(buildInFunction.Value.Name, buildInFunction.Key)
                    .AddParameterListParameters(CreateParameter("IDurableOrchestrationContext", "context"))
                    .WithExpressionBody(SF.ArrowExpressionClause(SF.ParseExpression(expression)))
                    .WithSemicolonToken(SF.Token(SyntaxKind.SemicolonToken))
                    .WithTrailingTrivia(SF.CarriageReturnLineFeed, SF.CarriageReturnLineFeed);

                yield return method;
            }

            foreach (var buildInFunction in ExpressionCompiler.BuildInContextlessFunctionTypeMap)
            {
                string expression = ExpressionCompiler.BuildInContextlessFunctionExpressionMap[buildInFunction.Key];
                MethodDeclarationSyntax method = CreateStaticMethod(buildInFunction.Value.Name, buildInFunction.Key)
                    .AddParameterListParameters(CreateParameter("string", "input"))
                    .WithExpressionBody(SF.ArrowExpressionClause(SF.ParseExpression(expression)))
                    .WithSemicolonToken(SF.Token(SyntaxKind.SemicolonToken))
                    .WithTrailingTrivia(SF.CarriageReturnLineFeed, SF.CarriageReturnLineFeed);

                yield return method;
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
                    return $"*/{recurrence.Interval} * * * * *";
                case "minute":
                    return $"* */{recurrence.Interval} * * * *";
                case "hour":
                    return $"* * */{recurrence.Interval} * * *";
                case "day":
                    return $"* * * */{recurrence.Interval} * *";
                case "month":
                    return $"* * * * */{recurrence.Interval} *";
                case "year":
                    return $"* * * * * */{recurrence.Interval}";
                default:
                    throw new ArgumentException($"Recurrence frequency '{recurrence.Frequency}' is not supported.");
            }
        }
    }
}
