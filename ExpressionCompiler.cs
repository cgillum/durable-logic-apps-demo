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
                            //if (i + 1 < stringBody.Length && stringBody[i + 1] == '{')
                            //{
                            //    endIndex = FindTheFirstClosingCurlyBracePosition(stringBody, i);
                            //    int removeIndex = stringBody.Substring(i, endIndex - i + 1).IndexOf('{');
                            //    var tryToEval = stringBody.Substring(i, endIndex - i + 1).Remove(removeIndex, 1);

                            //    hasStringInterpolation = hasStringInterpolation || ToStringInterpolation(tryToEval, out tempResult);
                            //    tempString += tempResult.ToString();

                            //    i = endIndex + 1;
                            //    continue;
                            //}

                            // pass int the expression, ex: @utcNow() => pass in utcNow() into ToStringInterpolation
                            tempString += '{';
                            int endIndex = FindThClosingParenthesisPosition(stringBody, i);
                            tempString += ToStringInterpolation(stringBody.Substring(i + 1, endIndex - i));

                            i = endIndex + 1;
                            continue;
                        }
                        //else if (currentChar == '[')
                        //{
                        //    //int endIndex = FindTheFirstClosingBracketPosition(stringBody, i);
                        //    //hasStringInterpolation = hasStringInterpolation ||  ConvertToStringInterpolation(stringBody.Substring(i, endIndex - i + 1), out tempResult);
                        //    //tempString += tempResult.ToString();
                        //    int endIndex = FindTheFirstClosingBracketPosition(stringBody, i);
                        //    var match = Regex.Match(stringBody.Substring(i, endIndex - i + 1), @"\['(\w+)'\]");
                        //    string propertyName = match.Groups[1].Value;

                        //    tempString += $@"[""{propertyName}""]";
                        //    i = endIndex + 1;
                        //    continue;
                        //}

                        tempString += currentChar;
                        i++;
                    }

                    // for bracket expression, simply replace ' with " and we will reserve that expression an evaluate at runtime
                    tempString = tempString.Replace('\'', '\"');

                    // for some expression we do not convert into string interpolation, so remove the curly braces at the end 
                    tempString = AddCurlyBracesPositionForExpressions(tempString);

                    return tempString;
                }
            }

            return input;
        }

        private static string AddCurlyBracesPositionForExpressions(string stringBody)
        {
            // clean up all upexpected '{' or '}'
            // stringBody = stringBody.RemoveChar("{").RemoveChar("}");

            // find the first @ sign index
            //int atSignIndex = FindTheFirstAtSignPositionFromStartIndex(stringBody, 0);
            int atSignIndex = FindTheFirstOpenCurlyBracesPositionFromStartIndex(stringBody, 0);

            while (atSignIndex < stringBody.Length)
            {
                //stringBody = stringBody.Insert(atSignIndex, "{");

                int closingIndex = FindTheClosingIndex(stringBody, atSignIndex + 1);

                if (closingIndex == stringBody.Length)
                {
                    stringBody += "}";
                }
                else
                {
                    stringBody = stringBody.Insert(closingIndex, "}");
                }

                atSignIndex = FindTheFirstAtSignPositionFromStartIndex(stringBody, closingIndex + 1);
            }

            return stringBody;
        }

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
            else if (expression.StartsWith("encodeURIComponent("))
            {
                match = Regex.Match(expression, @"encodeURIComponent\('(.+)'\)");
                if (!match.Success)
                {
                    throw new ArgumentException($"Error in regex match for @encodeURIComponent()");
                }

                string uriComponent = match.Groups[1].Value;

                return @$"@encodeURIComponent(""{uriComponent}"")";             
            }
            else if (expression.StartsWith("base64("))
            {
                match = Regex.Match(expression, @"base64\((.+)\)");
                if (!match.Success)
                {
                    throw new ArgumentException($"Error in regex match for @base64()");
                }

                string stringComponent = ExpressionCompiler.ToStringInterpolation(match.Groups[1].Value);
                return @$"@base64((string){(string)stringComponent})";
            }
            else if (expression.StartsWith("guid("))
            {
                return "@guid(context)";
            }
            else if (expression.StartsWith("utcNow("))
            {
                return "@utcNow(context)";
            }
            else
            {
                return expression;
                // need this to call recursively
                //throw new ArgumentException($"Didn't recognize expression: {expression}.");
            }
        }

        private static int FindTheFirstClosingParenthesisPosition(string str, int startIndex)
        {
            return startIndex + str.Substring(startIndex, str.Length - startIndex).IndexOf(')');
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

        private static int FindTheFirstClosingBracketPosition(string str, int startIndex)
        {
            return startIndex + str.Substring(startIndex, str.Length - startIndex).IndexOf(']');
        }

        private static int FindTheLastClosingBracketPosition(string str, int startIndex)
        {
            return startIndex + str.Substring(startIndex, str.Length - startIndex).LastIndexOf(']');
        }

        private static int FindTheFirstClosingCurlyBracePosition(string str, int startIndex)
        {
            return startIndex + str.Substring(startIndex, str.Length - startIndex).IndexOf('}');
        }

        private static int FindTheFirstOpenCurlyBracesPositionFromStartIndex(string str, int startIndex)
        {
            int index = str.Substring(startIndex, str.Length - startIndex).IndexOf('{');
            if (index < 0)
            {
                return str.Length;
            }
            return startIndex + index;
        }

        private static int FindTheFirstAtSignPositionFromStartIndex(string str, int startIndex)
        {
            int index = str.Substring(startIndex, str.Length - startIndex).IndexOf('@');
            if (index < 0)
            {
                return str.Length;
            }
            return startIndex + index;
        }

        private static int FindTheClosingIndex(string str, int openIndex)
        {
            int counter = 0;
            for (int index = openIndex + 1; index < str.Length; index++)
            {
                if(str[index] == '(' || str[index] == '[' || str[index] == '{')
                {
                    counter++;
                }

                if (str[index] == ')' || str[index] == ']' || str[index] == '}')
                {
                    counter--;

                    if (counter == 0 && index + 1 < str.Length && str[index + 1] != '[')
                    {
                        return index + 1;
                    }
                }               
            }
            return str.Length;
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
    }
}
