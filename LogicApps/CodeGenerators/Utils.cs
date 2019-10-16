using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogicApps.CodeGenerators
{
    internal static class Utils
    {
        public static string CreateJsonStringLiteral(JToken json)
        {
            if (json == null || json.Type == JTokenType.Null)
            {
                return "null";
            }

            return "@\"" + json.ToString(Formatting.None).Replace("\"", "\"\"") + "\"";
        }
    }
}
