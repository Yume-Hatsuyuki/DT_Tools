using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Experience.CanShowLie
{
    /// <summary>
    /// BuildLieSection 前置：非黑幕没有收到过 S_CURRENT_MAP，RoomObjectDict 为空；
    /// 用缓存的 S_INIT_MAP 数据构造等价包喂给 InitBlackPropositionData（0.1.15b ClueManager.cs:35）。
    /// 私有方法，字符串定位：0.1.15b UI_GameTablet.cs:2372。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameTablet), "BuildLieSection")]
    internal static class CanShowLieBuildPatch
    {
        private static void Prefix()
        {
            if (!Engine.Enabled<CanShowLieFeature>())
                return;

            if (Managers.Player.MyPlayer == null || Managers.Clue.RoomObjectDict.Count != 0)
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
