using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.IgnoreDarkness
{
    [HarmonyPatch(typeof(GameManagerEX), nameof(GameManagerEX.ApplyDarkness))]
    internal static class IgnoreDarknessApplyPatch
    {
        private static bool Prefix()
        {
            if (!Engine.Enabled<IgnoreDarknessFeature>())
                return true;

            IgnoreDarknessLogic.ForceLit();
            return false;
        }
    }

    [HarmonyPatch(typeof(Util), nameof(Util.CheckValidWithLight))]
    internal static class IgnoreDarknessValidPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Engine.Enabled<IgnoreDarknessFeature>())
                return true;

            __result = true;
            return false;
        }
    }
}
