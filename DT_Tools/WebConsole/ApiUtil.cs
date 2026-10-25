using System.Net;
using Newtonsoft.Json.Linq;

namespace DT_Tools.WebConsole
{
    internal static class ApiUtil
    {
        public static JObject ParseBody(HttpListenerContext ctx)
        {
            string body = HttpServer.ReadBody(ctx.Request);
            if (string.IsNullOrWhiteSpace(body))
                return null;
            try
            {
                return JObject.Parse(body);
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                return null;
            }
        }

        public static bool? ReadBool(JObject body, string key)
        {
            var token = body[key];
            if (token == null || token.Type != JTokenType.Boolean)
                return null;
            return (bool)token;
        }

        public static string NormalizeRaw(JToken token)
        {
            switch (token?.Type)
            {
                case null:
                case JTokenType.Null:
                    return null;
                case JTokenType.String:
                    return (string)token;
                case JTokenType.Boolean:
                    return (bool)token ? "true" : "false";
                default:
                    return token.ToString(Newtonsoft.Json.Formatting.None);
            }
        }
    }
}
