using System.Collections.Generic;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.GamePlay
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   UI_GameTablet::CanShowLie()
    ///   UI_GameTablet::IsMyPlayerBlack()   (0.1.13h：伪证页真正的功能门禁)
    ///   UI_GameTablet::BuildLieSection()
    ///   MapManager::LoadAllArea(S_INIT_MAP)
    ///
    /// <b>原版效果（0.1.13h）</b>：
    ///   LieTab 已在审判阶段对所有玩家原生可见（IsLieTabVisible），
    ///   但进入 LieSection 时 IsMyPlayerBlack()==false 的玩家只会看到
    ///   LieBlockPanel（LieBlockOnlyBlack）：LieList 隐藏、地图不可点、
    ///   不构建伪证内容。CanShowLie 也仅当 Color == Black，
    ///   控制地图射线开关与时间钉布局。
    ///   RoomObjectDict 由 ClueManager.InitBlackPropositionData(S_CURRENT_MAP) 填充；
    ///   服务端 GameRoom.StartDetective 只把 S_CURRENT_MAP 发给 Black，
    ///   其余颜色的字典为空。
    ///
    /// <b>修改后效果（任意身份伪证）</b>：
    ///   IsMyPlayerBlack / CanShowLie 去掉颜色判断，任何玩家在审判讨论阶段
    ///   且未提交时都能进入伪证编辑：隐藏拦截面板、显示伪证列表、可点击地图。
    ///   进入 BuildLieSection 时若 RoomObjectDict 为空（非 Black 均为空），
    ///   用缓存的 S_INIT_MAP.AreaInfos 构造 S_CURRENT_MAP 并调用 InitBlackPropositionData。
    ///   已验证提交链路无颜色门禁：UI_ClueSubItem.CanSubmitProposition 只看
    ///   审判讨论阶段/存活/未提交；服务端 TrialManager._candidates 包含所有
    ///   存活非旁观者且 SubmitProposition 不校验颜色，提交可正常广播
    ///   （仅伪证奖励 OnPerjury 仍只判定 Black；旁观者因 IsAlive=false 无法提交）。
    ///
    /// <b>修改方式</b>：
    ///   LoadAllArea Postfix 缓存 AreaInfos；
    ///   CanShowLie / IsMyPlayerBlack Prefix 去掉颜色条件；
    ///   BuildLieSection void Prefix 对任何字典为空的玩家补数据后继续原方法。
    /// </summary>
    [HarmonyPatch]
    [PatchConfig(
        "CanShowLie",
        "伪证解锁：审判讨论阶段任何身份都可使用伪证，可选内容与黑方一致。"+"\n二阶堂希罗：我当时睡得可香了。",
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

            // 原版还有 Color == Black 条件；此处去掉颜色判断，全员可在讨论阶段编辑伪证
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

        // 0.1.13h 门禁：StartSection 的 LieSection 分支用它决定
        // 显示 LieBlockPanel 还是真正的伪证列表（LieList / 地图射线 / RefreshLieSection）。
        // 原版仅 Color == Black；此处去掉颜色判断，任何玩家都走可编辑分支。
        // 旁观者虽也会进入编辑界面，但其提交按钮被 UI_ClueSubItem.CanSubmitProposition
        // 的 IsAlive 门禁禁用，服务端 _candidates 也不含旁观者。
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
            if (Managers.Player.MyPlayer == null)
                return;
            // S_CURRENT_MAP 服务端只发给 Black；任何字典为空的玩家都用 S_INIT_MAP 缓存补数据
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
