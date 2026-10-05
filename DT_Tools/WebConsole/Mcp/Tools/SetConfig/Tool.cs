using DT_Tools.Commands;
using DT_Tools.Core;
using DT_Tools.WebConsole.Api;
using Newtonsoft.Json.Linq;

namespace DT_Tools.WebConsole.Mcp.Tools.SetConfig
{
    /// <summary>set_config：改配置键并落盘。ConfigService.Update 本就供 HTTP 线程直调（同 /api/config/update）。</summary>
    [McpTool("set_config",
        "修改一个配置键并保存（类型必须匹配字段）。与 get_config 搭配：先读值再改。影响运行期行为，慎重。",
        Author = "梦初雪")]
    internal static class SetConfigTool
    {
        public static JObject Schema() => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["section"] = new JObject { ["type"] = "string", ["description"] = "配置段名（get_config 里的 section 字段）" },
                ["key"] = new JObject { ["type"] = "string", ["description"] = "配置键名（段内 entries 的 key 字段）" },
                ["value"] = new JObject { ["description"] = "新值（bool/number/string，类型须匹配字段定义）" },
                ["save"] = new JObject { ["type"] = "boolean", ["description"] = "是否立即写盘，默认 true" },
            },
            ["required"] = new JArray { "section", "key", "value" },
            ["additionalProperties"] = false,
        };

        public static CommandResult Execute(JObject args)
        {
            string section = args["section"]?.ToString();
            string key = args["key"]?.ToString();
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key))
                return CommandResult.Fail("missing section or key");

            string raw = ApiUtil.NormalizeRaw(args["value"]);
            if (raw == null)
                return CommandResult.Fail("missing value");

            var (ok, error, value) = ConfigService.Update(Engine.Config, section, key, raw);
            if (!ok)
                return CommandResult.Fail(error);

            bool save = true;
            if (args["save"] != null)
                bool.TryParse(args["save"].ToString(), out save);
            if (save)
                ConfigService.Save(Engine.Config);

            return CommandResult.Success(new { section, key, value, saved = save });
        }
    }
}
