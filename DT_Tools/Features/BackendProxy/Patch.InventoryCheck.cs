using System;
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.BackendProxy
{
    [HarmonyPatch(typeof(Define), nameof(Define.INVENTORY_CHECK_URL), MethodType.Getter)]
    internal static class BackendProxyInventoryCheckPatch
    {
        private static void Postfix(ref string __result)
        {
            if (!Engine.Enabled<BackendProxyFeature>())
                return;

            string prefix = BackendProxyLogic.Normalize(BackendProxyFeature.PayBaseUrl);
            if (prefix == null)
                return;

            string origin = BackendProxyLogic.VanillaPayOrigin;
            if (__result == null || !__result.StartsWith(origin, StringComparison.Ordinal))
                return;

            __result = prefix + __result.Substring(origin.Length);
        }
    }
}
