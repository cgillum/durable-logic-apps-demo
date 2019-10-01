using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LogicApps
{
    internal static class Utils
    {
        internal static string SanitizeName(string name)
        {
            var buffer = new StringBuilder(name.Length);
            bool capitalizeNext = false;

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '(' || c == ')')
                {
                    // drop parenthesis
                    continue;
                }

                if (c == '$' || c == '_')
                {
                    capitalizeNext = i != 0;
                    continue;
                }

                buffer.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            }

            return buffer.ToString();
        }

        internal static string GetWorkflowParameterVariableName(string name)
        {
            return name + "Param";
        }

        internal static string GetWorkflowResultVariableName(string name)
        {
            return "resultOf" + name;
        }
    }
}
