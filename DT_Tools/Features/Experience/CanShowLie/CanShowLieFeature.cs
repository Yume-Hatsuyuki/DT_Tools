using System.Collections.Generic;
using HarmonyLib;
using Protocol;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 任意身份伪证：去掉 CanShowLie / IsMyPlayerBlack 的 Black 限制；
    /// 非 Black 无 S_CURRENT_MAP 时用缓存的 S_INIT_MAP 补全 RoomObjectDict。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "CanShowLie",
        description: "伪证：审判讨论阶段任何身份都可使用伪证，可选内容与黑方一致。\n二阶堂希罗：我当时睡得可香了。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class CanShowLieFeature
    {
        private static List<AreaInitInfo> _areaCache;

        [HarmonyPatch(typeof(MapManager), "LoadAllArea")]
        [HarmonyPostfix]
        private static void PostfixLoadAllArea(S_INIT_MAP pkt)
        {
            if (pkt?.AreaInfos != null && pkt.AreaInfos.Count > 0)
                _areaCache = new List<AreaInitInfo>(pkt.AreaInfos);
        }

        [HarmonyPatch(typeof(UI_GameTablet), "CanShowLie")]
        [HarmonyPrefix]
        private static bool PrefixCanShowLie(ref bool __result)
        {
            UI_TrialEvent trial = (Managers.UI.SceneUI as UI_GameScene)?.TrialUI;
            if (Managers.Game.State == EGameState.Trial
                && (trial == null || trial.State == ETrialState.Discuss)
                && Managers.Player.MyPlayer != null)
            {
                __result = !Managers.Game.IsSubmitProposition;
                return false;
            }
            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(UI_GameTablet), "IsMyPlayerBlack")]
        [HarmonyPrefix]
        private static bool PrefixIsMyPlayerBlack(ref bool __result)
        {
            __result = Managers.Player.MyPlayer != null;
            return false;
        }

        [HarmonyPatch(typeof(UI_GameTablet), "BuildLieSection")]
        [HarmonyPrefix]
        private static void PrefixBuildLieSection()
        {
            if (Managers.Player.MyPlayer == null || Managers.Clue.RoomObjectDict.Count != 0)
                return;
            if (_areaCache == null || _areaCache.Count == 0)
                return;

            var pkt = new S_CURRENT_MAP();
            foreach (AreaInitInfo info in _areaCache)
                pkt.AreaInfos.Add(info);
            Managers.Clue.InitBlackPropositionData(pkt);
        }
    }
}
