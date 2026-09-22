using HarmonyLib;
using DT_Tools.Core;

namespace DT_Tools.Features.Fun
{
    /// <summary>
    /// IsGhostView 恒 true（原版为 !IsAlive）。调试向，可能干扰状态机。
    /// </summary>
    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.IsGhostView))]
    [PatchFeature(
        section: "IsGhostView",
        description: "幽灵视角：（⚠️奇怪的功能）化身幽灵。可能影响正常对局，建议仅本地调试。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class GhostViewFeature
    {
        [HarmonyPrefix]
        private static bool Prefix(ref bool __result)
        {
            if (!FeatureGate.Enabled(typeof(GhostViewFeature)))
                return true;

            __result = true;
            return false;
        }
    }
}
