// 作用：实现出站接口反代的运行时逻辑；不负责补丁挂载与配置声明。
namespace DT_Tools.Features.BackendProxy
{
    internal static class BackendProxyLogic
    {
        internal const string VanillaPayOrigin = "https://deadlytrick.finalblow.org";

        internal const string VanillaDiscordOrigin = "https://discord.com";

        internal static string Normalize(string value)
        {
            string trimmed = (value ?? "").Trim().TrimEnd('/');
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
