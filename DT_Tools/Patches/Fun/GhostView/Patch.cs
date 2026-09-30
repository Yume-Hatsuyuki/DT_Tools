using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Fun.GhostView
{
    /// <summary>
    /// 原版 IsGhostView 返回 !Managers.Game.IsAlive（public override，可用 nameof）：
    /// 0.1.15b MyPlayer.cs:1884。整替为恒 true。
    /// </summary>
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
