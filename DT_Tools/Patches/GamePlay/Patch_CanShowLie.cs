using System.Collections.Generic;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.GamePlay
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   UI_GameTablet::CanShowLie()
    ///   UI_GameTablet::BuildLieSection()
    ///   MapManager::LoadAllArea(S_INIT_MAP)
    ///
    /// <b>原版效果</b>：
    ///   CanShowLie 仅当 MyPlayer.Color == Black。
    ///   RoomObjectDict 由 ClueManager.InitBlackPropositionData(S_CURRENT_MAP) 填充，
    ///   该包通常只发给 Black；Dark 的字典为空。
    ///
    /// <b>修改后效果</b>：
    ///   CanShowLie 允许 Black 或 Dark。
    ///   Dark 进入 BuildLieSection 时若 RoomObjectDict 为空，
    ///   用缓存的 S_INIT_MAP.AreaInfos 构造 S_CURRENT_MAP 并调用 InitBlackPropositionData。
    ///
    /// <b>修改方式</b>：
    ///   LoadAllArea Postfix 缓存 AreaInfos；
    ///   CanShowLie Prefix 扩展颜色条件；
    ///   BuildLieSection void Prefix 补数据后继续原方法。
    /// </summary>
    [HarmonyPatch]
    [PatchConfig(
        "Enable_CanShowLie",
        "黑幕伪证：允许黑幕在审判阶段使用伪证，可选内容与黑方一致。",
        author: "梦初雪")]
    internal static class Patch_CanShowLie
    {
        private static List<AreaInitInfo> _cachedAreaInfos;

        [HarmonyPatch(typeof(MapManager), "LoadAllArea")]
        [HarmonyPostfix]
        private static void PostfixLoadAllArea(S_INIT_MAP pkt)
        {
            if (pkt != null && pkt.AreaInfos != null && pkt.AreaInfos.Count > 0)
                _cachedAreaInfos = new List<AreaInitInfo>(pkt.AreaInfos);
        }

        [HarmonyPatch(typeof(UI_GameTablet), "CanShowLie")]
        [HarmonyPrefix]
        private static bool PrefixCanShowLie(ref bool __result)
        {
            UI_TrialEvent trial = (Managers.UI.SceneUI as UI_GameScene)?.TrialUI;

            // 原版仅 Color == Black；此处增加 Dark
            if (Managers.Game.State == EGameState.Trial
                && (trial == null || trial.State == ETrialState.Discuss)
                && Managers.Player.MyPlayer != null
                && (Managers.Player.MyPlayer.Color == EPlayerColor.Black
                    || Managers.Player.MyPlayer.Color == EPlayerColor.Dark))
            {
                __result = !Managers.Game.IsSubmitProposition;
                return false;
            }

            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(UI_GameTablet), "BuildLieSection")]
        [HarmonyPrefix]
        private static void PrefixBuildLieSection()
        {
            if (Managers.Player.MyPlayer == null)
                return;
            if (Managers.Player.MyPlayer.Color != EPlayerColor.Dark)
                return;
            if (Managers.Clue.RoomObjectDict.Count != 0)
                return;
            if (_cachedAreaInfos == null || _cachedAreaInfos.Count == 0)
                return;

            var pkt = new S_CURRENT_MAP();
            foreach (AreaInitInfo info in _cachedAreaInfos)
                pkt.AreaInfos.Add(info);

            Managers.Clue.InitBlackPropositionData(pkt);
        }
    }
}
