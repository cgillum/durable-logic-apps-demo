using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace LogicApps
{
    public static class LogicAppsCompiler
    {
        public static void Compile(string workflowName, TextWriter codeWriter)
        {
            // Resources for learning how to use Roslyn to generate code:
            // https://carlos.mendible.com/2017/03/02/create-a-class-with-net-core-and-roslyn/ (seems to be the most up-to-date)
            // http://roslynquoter.azurewebsites.net/ (use with caution - this creates excessively verbose Roslyn code - most .WithXXX() methods could be simplified to .AddXXX())

            var workspace = new AdhocWorkspace();
            var cu = SF.CompilationUnit()
                .AddUsing("System")
                .AddUsing("Microsoft.Azure.WebJobs.Extensions.DurableTask");

            var ns = SF.NamespaceDeclaration(SF.IdentifierName("LogicAppsDemoApp.GeneratedCode"));

            var @class = SF.ClassDeclaration(workflowName).AddModifiers(SF.Token(SyntaxKind.StaticKeyword));
            var orchestrationMethod = CreateFunction(typeof(Task), "Orchestrator", workflowName)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(CreateBindingParameter("OrchestrationTrigger", "IDurableOrchestrationContext", "context"))
                .AddBodyStatements();

            @class = @class.AddMembers(orchestrationMethod);
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

        static MethodDeclarationSyntax CreateFunction(Type returnType, string methodName, string functionName)
        {
            MethodDeclarationSyntax method = SF.MethodDeclaration(
                SF.IdentifierName(returnType.Name),
                SF.Identifier(methodName));

            AttributeArgumentSyntax functionNameArgument = SF.AttributeArgument(
                SF.LiteralExpression(SyntaxKind.StringLiteralExpression, SF.Literal(functionName)));

            AttributeSyntax attribute = SF.Attribute(SF.IdentifierName("FunctionName"))
                .AddArgumentListArguments(functionNameArgument);

            return method.AddAttributeLists(SF.AttributeList(SF.SingletonSeparatedList(attribute)));
        }

        static ParameterSyntax CreateBindingParameter(string attributeName, string parameterType, string parameterName)
        {
            var parameter = SF.Parameter(SF.Identifier("context")).WithType(SF.IdentifierName("IDurableOrchestrationContext"));
            var attribute = SF.Attribute(SF.IdentifierName("OrchestrationTrigger"));
            return parameter.AddAttributeLists(SF.AttributeList(SF.SingletonSeparatedList(attribute)));
        }
    }
}
