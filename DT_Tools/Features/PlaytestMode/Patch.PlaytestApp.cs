using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.PlaytestMode
{
    [HarmonyPatch(typeof(Define), nameof(Define.IsPlaytestApp), MethodType.Getter)]
    internal static class PlaytestModePlaytestAppPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Engine.Enabled<PlaytestModeFeature>())
                return true;

            __result = true;
            return false;
        }
    }
}
