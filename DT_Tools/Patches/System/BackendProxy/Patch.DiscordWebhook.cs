using System;
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.System.BackendProxy
{
    /// <summary>
    /// BugReporter.UrlFor 后置替换。私有静态方法，字符串定位：0.1.16b BugReporter.cs:42
    /// （三条 webhook 常量 :18-22 正文仅在 :46-48 引用；Send :92 与 IsConfigured :64
    /// 都经它取址——一处后置即覆盖 Bug/建议/玩家举报全部上报）。
    /// 配置留空直通原版返回值；填入非空值时整替原版 origin（固定 https://discord.com，:18）
    /// 并原样保留其后的游戏路径 /api/webhooks/…（webhook id 与 token 在其中）。
    /// 反代转发到哪、用什么路径，由各人自建服务自行决定，此处不做任何约定。
    /// </summary>
    [HarmonyPatch(typeof(BugReporter), "UrlFor")]
    internal static class BackendProxyDiscordWebhookPatch
    {
        private static void Postfix(ref string __result)
        {
            if (!Engine.Enabled<BackendProxyFeature>())
                return;

            string prefix = BackendProxyLogic.Normalize(BackendProxyFeature.DiscordBaseUrl);
            if (prefix == null)
                return;

            string origin = BackendProxyLogic.VanillaDiscordOrigin;
            if (__result == null || !__result.StartsWith(origin, StringComparison.Ordinal))
                return;

            __result = prefix + __result.Substring(origin.Length);
        }
    }
}
