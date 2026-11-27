// 作用：为伪证挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.CanShowLie
{
    [HarmonyPatch(typeof(UI_GameTablet), "BuildLieSection")]
    internal static class CanShowLieBuildPatch
    {
        private static void Prefix()
        {
            if (!Engine.Enabled<CanShowLieFeature>())
                return;

            if (Managers.Player.MyPlayer == null || Managers.Clue.LieRoomSet.Count != 0)
                return;
            if (CanShowLieState.AreaCache == null || CanShowLieState.AreaCache.Count == 0)
                return;

            var pkt = new S_CURRENT_MAP();
            foreach (AreaInitInfo info in CanShowLieState.AreaCache)
                pkt.AreaInfos.Add(info);
            Managers.Clue.InitBlackPropositionData(pkt);
        }
    }
}
