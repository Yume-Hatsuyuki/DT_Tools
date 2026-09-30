using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.IgnoreDarkness
{
    /// <summary>
    /// ApplyDarkness 整替为恒亮（0.1.15b GameManagerEX.cs:260）。
    /// 原版：IsAlive &amp;&amp; _darkness → 玩家点光源开、全局光关。
    /// </summary>
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

    /// <summary>
    /// CheckValidWithLight 恒 true（0.1.15b Util.cs:774）。
    /// 原版黑暗时 distance &lt; 224 才有效，用于尸体发现、他人特效显隐等。
    /// </summary>
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
