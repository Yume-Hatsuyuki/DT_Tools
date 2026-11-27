// 作用：为伪证挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;
using System.Collections.Generic;
using Protocol;

namespace DT_Tools.Features.CanShowLie
{
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
