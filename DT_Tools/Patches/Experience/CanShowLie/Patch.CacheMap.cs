using DT_Tools.Core;
using HarmonyLib;
using System.Collections.Generic;
using Protocol;

namespace DT_Tools.Patches.Experience.CanShowLie
{
    /// <summary>
    /// LoadAllArea 后置：缓存 S_INIT_MAP.AreaInfos。
    /// 公共方法（nameof 定位），0.1.15b MapManager.cs:150。
    /// </summary>
    [HarmonyPatch(typeof(MapManager), nameof(MapManager.LoadAllArea))]
    internal static class CanShowLieCachePatch
    {
        private static void Postfix(S_INIT_MAP pkt)
        {
            if (!Engine.Enabled<CanShowLieFeature>())
                return;

            if (pkt?.AreaInfos != null && pkt.AreaInfos.Count > 0)
                CanShowLieState.AreaCache = new List<AreaInitInfo>(pkt.AreaInfos);
        }
    }
}
