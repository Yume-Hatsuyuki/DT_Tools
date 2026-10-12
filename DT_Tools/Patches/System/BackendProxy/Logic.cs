namespace DT_Tools.Patches.System.BackendProxy
{
    /// 反代前缀的归一化。唯一的防呆是空值回退原版；不做 URL 解析、
    /// 不与任何特定反代的路径约定耦合（各人自建反代的路径自行决定）。
    /// 原版 origin 锚点：库存校验 0.1.17a Define.cs:384/:386（/pay、/pay-dev 同源），
    /// Discord webhook 0.1.17a BugReporter.cs 常量。
    internal static class BackendProxyLogic
    {
        /// 游戏原版支付/库存后端 origin（0.1.17a Define.cs:380/:382/:384/:386，正式/测试同源）。
        internal const string VanillaPayOrigin = "https://deadlytrick.finalblow.org";

        /// 游戏原版 Discord 上报 origin（BugReporter webhook 常量，三条同源）。
        internal const string VanillaDiscordOrigin = "https://discord.com";

        /// 去首尾空白与结尾斜杠（纯防呆：避免拼出 //api/… 双斜杠）。
        /// 空串返回 null —— 补丁直通原版返回值，等同未安装本功能。
        internal static string Normalize(string value)
        {
            string trimmed = (value ?? "").Trim().TrimEnd('/');
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
