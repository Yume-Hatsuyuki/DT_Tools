using System;
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.BackendProxy
{
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
