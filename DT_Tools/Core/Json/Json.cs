using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DT_Tools.Core
{
    public static class Json
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static string To(object value)
            => JsonConvert.SerializeObject(value, Formatting.None, Settings);

        public static T From<T>(string json)
            => JsonConvert.DeserializeObject<T>(json);

        public static bool TryFrom<T>(string json, out T value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                value = From<T>(json);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
