namespace LogicApps
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    static class ExpressionCompiler
    {
        public static string ConvertToStringInterpolation(JToken token)
        {
            string interpolated = ToJsonString(token, 0);
            return $@"$@""{interpolated}""";
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
                    bool hasStringInterpolation = false;
                    while (i < stringBody.Length)
                    {
                        char currentChar = stringBody[i];
                        if (currentChar == '@')
                        {
                            int endIndex;

                            if (i + 1 < stringBody.Length && stringBody[i + 1] == '{')
                            {
                                endIndex = FindTheFirstClosingCurlyBracePosition(stringBody, i);
                                int removeIndex = stringBody.Substring(i, endIndex - i + 1).IndexOf('{');
                                var tryToEval = stringBody.Substring(i, endIndex - i + 1).Remove(removeIndex, 1);

                                hasStringInterpolation = hasStringInterpolation || ConvertToStringInterpolation(tryToEval, out tempResult);
                                tempString += tempResult.ToString();

                                i = endIndex + 1;
                                continue;
                            }

                            endIndex = FindTheFirstClosingParenthesisPosition(stringBody, i);
                            hasStringInterpolation = hasStringInterpolation || ConvertToStringInterpolation(stringBody.Substring(i, endIndex - i + 1), out tempResult);
                            tempString += tempResult.ToString();

                            i = endIndex + 1;
                            continue;
                        }
                        else if (currentChar == '[')
                        {
                            //int endIndex = FindTheFirstClosingBracketPosition(stringBody, i);
                            //hasStringInterpolation = hasStringInterpolation ||  ConvertToStringInterpolation(stringBody.Substring(i, endIndex - i + 1), out tempResult);
                            //tempString += tempResult.ToString();
                            int endIndex = FindTheFirstClosingBracketPosition(stringBody, i);
                            var match = Regex.Match(stringBody.Substring(i, endIndex - i + 1), @"\['(\w+)'\]");
                            string propertyName = match.Groups[1].Value;

                            tempString += $@"[""{propertyName}""]";
                            i = endIndex + 1;
                            continue;
                        }

                        tempString += currentChar;
                        i++;
                    }


                    if (hasStringInterpolation)
                    {
                        int lastclosingBracketIndex = FindTheLastClosingBracketPosition(tempString, 0);
                        if (lastclosingBracketIndex == tempString.Length -1)
                        {
                            tempString += "}";
                        }
                        else
                        {
                            tempString = tempString.Insert(lastclosingBracketIndex + 1, "}");
                        }                       
                    }
                    
                    return tempString;
                }
            }

            return input;
        }

        private static bool ConvertToStringInterpolation(string expression, out JToken result)
        {
            Match match;
            if (expression.StartsWith("@outputs("))
            {
                match = Regex.Match(expression, @"@outputs\('(\w+)'\)");
                string outputName = match.Groups[1].Value;
                result = $@"{{Outputs[""{outputName}""]";
                return true;
            }
            else if (expression.StartsWith("@parameters("))
            {
                match = Regex.Match(expression, @"@parameters\('(\$\w+)'\)");
                string parameterName = match.Groups[1].Value;
                result = $@"{{Parameters[""{parameterName}""]";
                return true;
            }
            else if (expression.StartsWith("["))
            {
                match = Regex.Match(expression, @"\['(\w+)'\]");
                string propertyName = match.Groups[1].Value;
                result = $@"[""{propertyName}""]";
                return true;
            }
            else if (expression.StartsWith("@encodeURIComponent("))
            {
                match = Regex.Match(expression, @"@encodeURIComponent\('(.+)'\)");
                if (!match.Success)
                {
                    throw new ArgumentException($"Error in regex match for @encodeURIComponent()");
                }

                string uriComponent = match.Groups[1].Value;
                result = $"{Uri.EscapeUriString(uriComponent)}";
                return false;
            }
            else if(expression.StartsWith("@guid("))
            {
                result = "{@guid(context)}";
                return false;
            }
            else if (expression.StartsWith("@utcNow("))
            {
                result = "{@utcNow(context)}";
                return false;
            }
            else
            {
                throw new ArgumentException($"Didn't recognize expression: {expression}.");
            }
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

        public static Dictionary<string, string> BuildInFunctionStatementMap = new Dictionary<string, string>
        {
            {"@guid", "context.NewGuid()"},
            {"@utcNow", "context.CurrentUtcDateTime"},
        };

        public static Dictionary<string, Type> BuildInFunctionTypeMap = new Dictionary<string, Type>
        {
            {"@guid", typeof(Guid)},
            {"@utcNow", typeof(DateTime)},
        };
    }
}
