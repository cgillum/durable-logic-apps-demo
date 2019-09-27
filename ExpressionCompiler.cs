namespace LogicApps
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    static class ExpressionCompiler
    {
        public static string ConvertJTokenToStringInterpolation(JToken token)
        {
            string interpolated = ToJsonString(token, 0);
            return $@"$@""{interpolated}""";
        }

        public static string ConvertStringToStringInterpolation(string input)
        {
            return $@"$@""""""{ToJsonString(input, 0)}""""""";
        }

        private static string ToJsonString(JToken input, int level)
        {
            if (input.Type == JTokenType.Property)
            {
                JProperty jProperty = (JProperty)input;
                if (jProperty.HasValues)
                {
                    return $@"""""{jProperty.Name}"""": {ToJsonString(jProperty.Value, ++level)}";
                }
                else
                {
                    return $@"""""{jProperty.Name}"""""": null";
                }
            }
            else if (input.Type == JTokenType.Object)
            {
                List<string> results = new List<string>();
                ++level;
                foreach (var token in input)
                {
                    results.Add(ToJsonString(token, level));
                }
                return $@"{{{{{string.Join(",", results)}}}}}";
            }
            else if (input.Type == JTokenType.Array)
            {
                List<string> results = new List<string>();
                foreach (var token in input)
                {
                    results.Add(ToJsonString(token, level));
                }
                return $@"[{string.Join(",", results)}]";
            }
            else
            {
                // reach the bottom layer and should expand the value            
                JToken value = ParseToken(input).ToString();

                if (level == 0)
                {
                    return $@"{value}";
                }
                else
                {
                    return $@"""""{value}""""";
                }               
            }
        }

        private static JToken ParseToken(JToken input)
        {
            // Check to see if this is an expression
            if (input.Type == JTokenType.String)
            {
                string stringBody = (string)input;
                if (stringBody?.Length > 0)
                {
                    int i = 0;
                    JToken tempResult = input;
                    string tempString = "";
                    stringBody = stringBody.RemoveChar("{").RemoveChar("}");

                    while (i < stringBody.Length)
                    {
                        char currentChar = stringBody[i];
                        if (currentChar == '@')
                        {
                            // append a open brace to indicate here begins a string interpolation
                            tempString += '{';

                            // pass the expression without @ sign, ex: @utcNow() => pass in utcNow() into ToStringInterpolation
                            int endIndex = FindThClosingParenthesisPosition(stringBody, i);
                            tempString += ToStringInterpolation(stringBody.Substring(i + 1, endIndex - i));

                            i = endIndex + 1;
                            continue;
                        }

                        tempString += currentChar;
                        i++;
                    }

                    // for bracket expression, simply replace ' with " and we will reserve that expression an evaluate at runtime
                    tempString = tempString.Replace('\'', '\"');

                    // for some expression we do not convert into string interpolation, so remove the curly braces at the end 
                    tempString = CloseCurlyBraceForStringInterpolation(tempString);

                    return tempString;
                }
            }

            return input;
        }

        /// <summary>
        /// Converts a workflow expression into a C# expression.
        /// </summary>
        /// <param name="expression">The expression to convert.</param>
        /// <returns>
        /// Returns current expression
        /// </returns>
        private static string ToStringInterpolation(string expression)
        {
            Match match;
            if (expression.StartsWith("outputs("))
            {
                match = Regex.Match(expression, @"outputs\('(\w+)'\)");
                string outputName = match.Groups[1].Value;
                return $@"@Outputs[""{outputName}""]";
            }
            else if (expression.StartsWith("parameters("))
            {
                match = Regex.Match(expression, @"parameters\('(\$\w+)'\)");
                string parameterName = match.Groups[1].Value;
                return $@"@Parameters[""{parameterName}""]";
            }
            else if (expression.StartsWith("triggerBody("))
            {
                // TODO: This doesn't currently work with expressions like "@triggerBody().name"
                return "@triggerBody(context)";
            }
            else if (expression.StartsWith("encodeURIComponent("))
            {
                match = Regex.Match(expression, @"encodeURIComponent\('(.+)'\)");
                if (!match.Success)
                {
                    throw new ArgumentException($"Error in regex match for @encodeURIComponent()");
                }

                string uriComponent = match.Groups[1].Value;
                return $@"@encodeURIComponent(""{uriComponent}"")";
            }
            else if (expression.StartsWith("base64("))
            {
                match = Regex.Match(expression, @"base64\((.+)\)");
                if (!match.Success)
                {
                    throw new ArgumentException($"Error in regex match for @base64()");
                }

                // todo: does not support expression ex: @base64(output('Compose'))
                // has to call recursively on this function to support, but current base case throw instead return
                //ex code: string stringComponent = ExpressionCompiler.ToStringInterpolation(match.Groups[1].Value);
                string stringComponent = match.Groups[1].Value;
                return $@"@base64((string){(string)stringComponent})";
            }
            else if(expression.StartsWith("guid("))
            {
                return "{@guid(context)}";
            }
            else if (expression.StartsWith("utcNow("))
            {
                return "{@utcNow(context)}";
            }
            else
            {
                throw new ArgumentException($"Didn't recognize expression: {expression}.");
            }
        }

        public static Dictionary<string, string> BuildInFunctionStatementMap = new Dictionary<string, string>
        {
            {"@guid", "context.NewGuid()"},
            {"@utcNow", "context.CurrentUtcDateTime"},
            {"@triggerBody", "context.GetInput<JToken>()"},
        };

        public static Dictionary<string, Type> BuildInFunctionTypeMap = new Dictionary<string, Type>
        {
            {"@guid", typeof(Guid)},
            {"@utcNow", typeof(DateTime)},
            {"@triggerBody", typeof(JToken)},
        };

        public static Dictionary<string, string> BuildInContextlessFunctionStatementMap = new Dictionary<string, string>
        {
            {"@encodeURIComponent", "Uri.EscapeUriString(content)"},
            { "@base64", "Convert.ToBase64String(Encoding.UTF8.GetBytes(content))"}
        };

        public static Dictionary<string, Type> BuildInContextlessFunctionTypeMap = new Dictionary<string, Type>
        {
            {"@encodeURIComponent", typeof(string)},
            { "@base64", typeof(string)},
        };

        private static string RemoveChar(this string str, string c)
        {
            return str.Replace(c, string.Empty);
        }

        private static string CloseCurlyBraceForStringInterpolation(string stringBody)
        {
            // find the first { sign index
            int atSignIndex = FindTheFirstOpenCurlyBracePositionFromStartIndex(stringBody, 0);

            while (atSignIndex < stringBody.Length)
            {
                int closingIndex = FindTheClosingCurlyBraceIndex(stringBody, atSignIndex + 1);

                if (closingIndex == stringBody.Length)
                {
                    stringBody += "}";
                }
                else
                {
                    stringBody = stringBody.Insert(closingIndex, "}");
                }

                atSignIndex = FindTheFirstOpenCurlyBracePositionFromStartIndex(stringBody, closingIndex + 1);
            }

            return stringBody;
        }

        private static int FindTheFirstOpenCurlyBracePositionFromStartIndex(string str, int startIndex)
        {
            int index = str.Substring(startIndex, str.Length - startIndex).IndexOf('{');
            if (index < 0)
            {
                return str.Length;
            }
            return startIndex + index;
        }

        private static int FindThClosingParenthesisPosition(string str, int startIndex)
        {
            int counter = 0;
            for (int index = startIndex; index < str.Length; index++)
            {
                if (str[index] == '(')
                {
                    counter++;
                }

                if (str[index] == ')')
                {
                    counter--;

                    if (counter == 0)
                    {
                        return index;
                    }
                }
            }
            return str.Length;
        }

        private static int FindTheClosingCurlyBraceIndex(string str, int openIndex)
        {
            int counter = 0;
            for (int index = openIndex + 1; index < str.Length; index++)
            {
                if (str[index] == '(' || str[index] == '[' || str[index] == '{')
                {
                    counter++;
                }

                if (str[index] == ')' || str[index] == ']' || str[index] == '}')
                {
                    counter--;

                    if (counter == 0 && index + 1 < str.Length && (str[index + 1] != '[' && str[index + 1] != '?' && str[index + 1] != '.'))
                    {
                        return index + 1;
                    }
                }
            }
            return str.Length;
        }


        private static int FindTheFirstClosingParenthesisPosition(string str, int startIndex)
        {
            return startIndex + str.Substring(startIndex, str.Length - startIndex).IndexOf(')');
        }

        private static int FindTheFirstClosingBracketPosition(string str, int startIndex)
        {
            return startIndex + str.Substring(startIndex, str.Length - startIndex).IndexOf(']');
        }

        private static int FindTheFirstClosingCurlyBracePosition(string str, int startIndex)
        {
            return startIndex + str.Substring(startIndex, str.Length - startIndex).IndexOf('}');
        }

        private static int FindTheLastClosingBracketPosition(string str, int startIndex)
        {
            return startIndex + str.Substring(startIndex, str.Length - startIndex).LastIndexOf(']');
        }
    }
}
