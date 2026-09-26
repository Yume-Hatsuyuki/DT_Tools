using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Experience.CanShowLie
{
    /// <summary>
    /// CanShowLie 整替：去掉原版的 Color==Black 限制（原版判定见 0.1.15b UI_GameTablet.cs:980）。
    /// 私有方法，字符串定位；其余条件（Trial + 讨论阶段 + 有本地玩家）与原版一致。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameTablet), "CanShowLie")]
    internal static class CanShowLieGatePatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Engine.Enabled<CanShowLieFeature>())
                return true;

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
    }
}
