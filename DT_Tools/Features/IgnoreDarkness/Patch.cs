using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.IgnoreDarkness
{
    /// <summary>
    /// ApplyDarkness 整替为恒亮（0.1.18c GameManagerEX.cs:260）。
    /// </summary>
    /// <remarks>
    /// 原版：IsAlive &amp;&amp; _darkness → 玩家点光源开、全局光关。
    /// </remarks>
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
    /// CheckValidWithLight 恒 true（0.1.18c Util.cs:774）。
    /// </summary>
    /// <remarks>
    /// 原版黑暗时 distance &lt; 224 才有效，用于尸体发现、他人特效显隐等。
    /// </remarks>
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
