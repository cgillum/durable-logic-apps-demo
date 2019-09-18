using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using System.Reflection;

namespace LogicApps
{
    class CodeDomCodeGenerator
    {
        public static void Generate(string workflowName, TextWriter codeWriter)
        {
            // TODO: Consider replacing this with Roslyn
            var code = new CodeCompileUnit()
            {
                Namespaces =
                {
                    new CodeNamespace("LogicAppsDemoApp.GeneratedCode")
                    {
                        Imports =
                        {
                            new CodeNamespaceImport("System"),
                            new CodeNamespaceImport("Microsoft.Azure.WebJobs.Extensions.DurableTask"),
                        },
                        Types =
                        {
                            new CodeTypeDeclaration(workflowName)
                            {
                                // NOTE: CodeDom doesn't support static classes
                                IsClass = true,
                                TypeAttributes = TypeAttributes.Sealed,
                                Members =
                                {
                                    GenerateOrchestratorFunction(workflowName),
                                    ////GenerateTriggerFunction(),
                                },
                            },
                        },
                    },
                },
            };

            var options = new CodeGeneratorOptions()
            {
                BracingStyle = "C",
            };

            var codeProvider = CodeDomProvider.CreateProvider("CSharp");
            codeProvider.GenerateCodeFromCompileUnit(code, codeWriter, options);
        }

        static CodeMemberMethod GenerateOrchestratorFunction(string workflowName)
        {
            return new CodeMemberMethod()
            {
                Name = "Orchestration",
                Attributes = MemberAttributes.Public | MemberAttributes.Static,
                CustomAttributes =
                {
                    new CodeAttributeDeclaration(
                        "FunctionName",
                        new [] { new CodeAttributeArgument(new CodePrimitiveExpression(workflowName)) }),
                },
                Parameters =
                {
                    new CodeParameterDeclarationExpression("IDurableOrchestrationContext", "context")
                    {
                        CustomAttributes =
                        {
                            new CodeAttributeDeclaration("OrchestrationTrigger")
                        }
                    }
                }
            };
        }

        static CodeMemberMethod GenerateTriggerFunction()
        {
            throw new NotImplementedException();
        }
    }
}
