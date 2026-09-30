using Newtonsoft.Json;

namespace DT_Tools.Commands
{
    /// <summary>
    /// 命令结果信封：Newtonsoft 序列化为 {"ok":…,"error":…,"data":…}（camelCase）。
    /// data 用匿名对象 / DTO（键名即想要的 JSON 形状），禁止字符串拼接。
    /// 错误码约定：小写英文短词（"host only"、"no room"、"invalid target"），与前端约定一致。
    /// </summary>
    public sealed class CommandResult
    {
        public bool Ok { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Error { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; private set; }

        private CommandResult(bool ok, string error, object data)
        {
            Ok = ok;
            Error = error;
            Data = data;
        }

        public static CommandResult Success(object data = null) => new CommandResult(true, null, data);

        public static CommandResult Fail(string error, object data = null)
            => new CommandResult(false, string.IsNullOrEmpty(error) ? "error" : error, data);
    }
}
