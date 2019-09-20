namespace LogicApps
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    static class ExpressionCompiler
    {
        public static string ConvertToStringInterpolation(JToken token)
        {
            //JToken json = Evaluate(token);
            //string interpolated = JsonConvert.SerializeObject(json).Replace("\"", "\"\"");
            //return $@"$@""{{{interpolated}}}""";
            string interpolated = ToJsonString(token).Replace("\"", "\"\"");
            return $@"$@""{interpolated}""";
        }

        public static string ToJsonString(JToken input)
        {
            if (input.Type == JTokenType.Property)
            {
                JProperty jProperty = (JProperty)input;
                if (jProperty.HasValues)
                {
                    return $@"""{jProperty.Name}"":""{ToJsonString(jProperty.Value)}""";
                }
                else
                {
                    return $@"""{jProperty.Name}"""":""null""";
                }
            }
            else if (input.Type == JTokenType.Object)
            {
                List<string> results = new List<string>(); 
                foreach (var token in input)
                {
                    results.Add(ToJsonString(token));
                }
                return $@"{{{{{string.Join(",", results)}}}}}";
            }
            else if (input.Type == JTokenType.Array)
            {
                List<string> results = new List<string>();
                foreach (var token in input)
                {
                    results.Add(ToJsonString(token));
                }
                return $@"[{string.Join(",", results)}]";
            }
            else
            {
                // reach the bottom layer and should expand the value
                return ParseToken(input).ToString();
            }
        }

        public static JToken Evaluate(JToken input)
        {
            if (input.Type == JTokenType.Property)
            {
                JProperty jProperty = (JProperty)input;
                if (jProperty.HasValues)
                {
                    return new JProperty(jProperty.Name, Evaluate(jProperty.Value));
                }
                else
                {
                    return new JProperty(jProperty.Name, null);
                }
            }
            else if (input.Type == JTokenType.Object)
            {
                JObject resultObect = new JObject();
                foreach (var token in input)
                {
                    resultObect.Add(Evaluate(token));
                }
                return resultObect;
            }
            else if (input.Type == JTokenType.Array)
            {
                JArray resultArray = new JArray();
                foreach (var token in input)
                {
                    resultArray.Add(Evaluate(token));
                }
                return resultArray;
            }
            else
            {
                // reach the bottom layer and should expand the value
                return ParseToken(input);
            }
        }

        public static JToken ParseToken(JToken input)
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
                    bool returnString = true;
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

                                tempResult = ConvertToStringInterpolation(tryToEval);
                                tempString += tempResult.ToString();

                                i = endIndex + 1;
                                continue;
                            }

                            endIndex = FindTheFirstClosingParenthesisPosition(stringBody, i);
                            tempResult = ConvertToStringInterpolation(stringBody.Substring(i, endIndex - i + 1));
                            tempString += tempResult.ToString();

                            i = endIndex + 1;
                            continue;
                        }
                        else if (currentChar == '[')
                        {
                            int endIndex = FindTheFirstClosingBracketPosition(stringBody, i);
                            var match = Regex.Match(stringBody.Substring(i, endIndex - i + 1), @"\['(\w+)'\]");
                            string propertyName = match.Groups[1].Value;

                            tempString += $@"[""{propertyName}""]";

                            //tempResult = ((JObject)tempResult)[propertyName];
                            //returnString = false;
                            i = endIndex + 1;
                            continue;
                        }

                        tempString += currentChar;
                        i++;
                    }

                    if (returnString)
                    {
                        return tempString;
                    }
                    else
                    {
                        return tempResult;
                    }
                }
            }

            return input;
        }

        private static JToken ConvertToStringInterpolation(string expression)
        {
            Match match;
            if (expression.StartsWith("@outputs("))
            {
                match = Regex.Match(expression, @"@outputs\('(\w+)'\)");
                string outputName = match.Groups[1].Value;
                return $@"Outputs[""{outputName}""]";
            }
            else if (expression.StartsWith("@parameters("))
            {
                match = Regex.Match(expression, @"@parameters\('(\$\w+)'\)");
                string parameterName = match.Groups[1].Value;
                return $@"Parameters[""{parameterName}""]";
            }
            else if (expression.StartsWith("@encodeURIComponent("))
            {
                match = Regex.Match(expression, @"@encodeURIComponent\('(.+)'\)");
                if (!match.Success)
                {
                    throw new ArgumentException($"Error in regex match for @encodeURIComponent()");
                }

                string uriComponent = match.Groups[1].Value;
                return $"{Uri.EscapeUriString(uriComponent)}";
            }
            else if(expression.StartsWith("@guid("))
            {
                return "{@guid(context)}";
            }
            else if (expression.StartsWith("@utcNow("))
            {
                return "{@utcNow(context)}";
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
    }
}
