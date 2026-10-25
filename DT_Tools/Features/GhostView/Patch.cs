using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.GhostView
{
    [HarmonyPatch(typeof(MyPlayer), nameof(MyPlayer.IsGhostView))]
    internal static class GhostViewPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Engine.Enabled<GhostViewFeature>())
                return true;

            __result = true;
            return false;
        }
    }
}
