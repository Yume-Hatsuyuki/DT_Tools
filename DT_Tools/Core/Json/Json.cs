using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DT_Tools.Core
{
    /// <summary>
    /// 全项目唯一 JSON 入口（Newtonsoft，游戏自带程序集）。
    /// 序列化对象用匿名对象或 DTO；禁止手拼 JSON 字符串与手写解析。
    /// </summary>
    public static class Json
    {
        /// <summary>API 输出统一设置：camelCase 键名、忽略 null 字段、紧凑格式。</summary>
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static string To(object value)
            => JsonConvert.SerializeObject(value, Formatting.None, Settings);

        public static string Pretty(object value)
            => JsonConvert.SerializeObject(value, Formatting.Indented, Settings);

        public static T From<T>(string json)
            => JsonConvert.DeserializeObject<T>(json);

        /// <summary>容错解析：失败返回 false（调用方应记日志，不要静默吞）。</summary>
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
