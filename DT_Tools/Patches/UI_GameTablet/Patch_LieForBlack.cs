using HarmonyLib;
using Protocol;
using System.Collections.Generic;

namespace DT_Tools.Patches
{
    /// <summary>
    /// 修改位置：System.Boolean UI_GameTablet::CanShowLie()
    ///           System.Boolean UI_GameTablet::BuildLieSection()（注入点）
    ///           MapManager::LoadAllArea(S_INIT_MAP)（缓存地图数据）
    /// 黑幕伪证支持
    /// 原逻辑：服务器在 StartDetective 时只向 Black 玩家发送 S_CURRENT_MAP 包，
    ///         客户端 Handle_S_CURRENT_MAP 收到后调用 ClueManager.InitBlackPropositionData(pkt)
    ///         初始化 RoomObjectDict。Dark（黑幕）从未收到该包，导致 RoomObjectDict 为空，
    ///         HasLieWhatOptions 返回 false，RefreshWhat 只显示 "-" 无法使用。
    /// 补丁逻辑：
    ///   1. CanShowLie 扩展权限：黑幕也可进入伪证界面
    ///   2. LoadAllArea Postfix：所有玩家都会收到 S_INIT_MAP（含与 S_CURRENT_MAP 相同结构的
    ///      AreaInfos），在此缓存一份，供黑幕后续构造 S_CURRENT_MAP 使用
    ///   3. BuildLieSection Prefix：若当前玩家是 Dark 且 RoomObjectDict 为空，
    ///      用缓存的 AreaInfos 构造 S_CURRENT_MAP，调用 InitBlackPropositionData(pkt) 补充初始化，
    ///      之后 HasLieWhatOptions / RefreshWhat 的原始逻辑完全不动，
    ///      自动读取当前地图真实存在的设备，与黑方体验一致。
    /// </summary>
    [HarmonyPatch]
    [PatchConfig("Enable_Patch_LieForBlack", "黑幕伪证支持：让黑幕(Dark)也能使用伪证功能，且物品列表与普通黑方完全一致，仅显示当前地图真实存在的设备。", author: "梦初雪")]
    internal static class Patch_LieForBlack
    {
        // ── 缓存 S_INIT_MAP 中的 AreaInfos（所有玩家都会收到 S_INIT_MAP） ──
        // S_INIT_MAP.AreaInfos 与 S_CURRENT_MAP.AreaInfos 结构完全相同（均为 RepeatedField<AreaInitInfo>），
        // 因此可以直接用来构造 S_CURRENT_MAP 传给 InitBlackPropositionData。
        private static List<AreaInitInfo> _cachedAreaInfos;

        // ── 0. 缓存地图初始化包中的区域设备信息 ──────────────────────────
        // MapManager.LoadAllArea(S_INIT_MAP pkt) 在客户端收到 S_INIT_MAP 时被调用，
        // 所有玩家（包括 Dark）都会走到这里。在此 Postfix 中缓存 AreaInfos。
        [HarmonyPatch(typeof(MapManager), "LoadAllArea")]
        [HarmonyPostfix]
        private static void PostfixLoadAllArea(S_INIT_MAP pkt)
        {
            if (pkt != null && pkt.AreaInfos != null && pkt.AreaInfos.Count > 0)
            {
                _cachedAreaInfos = new List<AreaInitInfo>(pkt.AreaInfos);
            }
        }

        // ── 1. 扩展伪证权限：黑幕和黑方均可进入伪证界面 ────────────────
        [HarmonyPatch(typeof(UI_GameTablet), "CanShowLie")]
        [HarmonyPrefix]
        private static bool PrefixCanShowLie(ref bool __result)
        {
            UI_GameScene ui_GameScene = Managers.UI.SceneUI as UI_GameScene;
            UI_TrialEvent ui_TrialEvent = (ui_GameScene != null) ? ui_GameScene.TrialUI : null;
            __result = Managers.Game.State == EGameState.Trial
                && (ui_TrialEvent == null || ui_TrialEvent.State == ETrialState.Discuss)
                && Managers.Player.MyPlayer != null
                && (Managers.Player.MyPlayer.Color == EPlayerColor.Black
                    || Managers.Player.MyPlayer.Color == EPlayerColor.Dark)
                && !Managers.Game.IsSubmitProposition;
            return false;
        }

        // ── 2. 在 BuildLieSection 前补充初始化 RoomObjectDict ──────────
        // BuildLieSection 是进入伪证面板时构建整个 UI 的入口，在它最开头注入。
        // 若玩家是 Dark 且 RoomObjectDict 尚未初始化（Count == 0），
        // 则用缓存的 AreaInfos 构造 S_CURRENT_MAP 并调用 InitBlackPropositionData(pkt)，
        // 使后续的 HasLieWhatOptions / RefreshWhat 走原版流程，自动只列出地图真实存在的设备。
        //
        // 注意：此 Prefix 返回 void，原方法在本方法执行完毕后会继续执行（数据补充，非替代）。
        [HarmonyPatch(typeof(UI_GameTablet), "BuildLieSection")]
        [HarmonyPrefix]
        private static void PrefixBuildLieSection()
        {
            if (Managers.Player.MyPlayer == null) return;
            if (Managers.Player.MyPlayer.Color != EPlayerColor.Dark) return;

            // RoomObjectDict 为空说明 InitBlackPropositionData 从未对黑幕执行过
            if (Managers.Clue.RoomObjectDict.Count == 0)
            {
                if (_cachedAreaInfos == null || _cachedAreaInfos.Count == 0)
                {
                    // 理论上不会发生：进入 Trial 前 S_INIT_MAP 必然已收到并缓存
                    return;
                }

                // 用缓存的 AreaInfos 构造 S_CURRENT_MAP
                S_CURRENT_MAP pkt = new S_CURRENT_MAP();
                foreach (AreaInitInfo info in _cachedAreaInfos)
                {
                    pkt.AreaInfos.Add(info);
                }

                Managers.Clue.InitBlackPropositionData(pkt);
            }
        }
    }
}
