using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.CanShowLie
{
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
