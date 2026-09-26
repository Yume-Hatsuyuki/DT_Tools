using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Dev.PlaytestMode
{
    /// <summary>
    /// IsPlaytestApp 整替为恒 true。公共静态属性（nameof 定位），
    /// 0.1.15b Define.cs:2013（原版按 Steam AppId 判定）。
    /// </summary>
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
