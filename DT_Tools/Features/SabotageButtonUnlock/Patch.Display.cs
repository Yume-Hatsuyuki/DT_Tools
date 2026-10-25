using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.SabotageButtonUnlock
{
    [HarmonyPatch(typeof(DeviceBase), nameof(DeviceBase.GetInteractSabotageMessageBase))]
    internal static class SabotageMessageBasePatch
    {
        private static bool Prefix(DeviceBase __instance, int index, ref string __result)
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return true;

            var color = Managers.Player.MyPlayer.Color;
            if (color == EPlayerColor.Dark || color == EPlayerColor.Black)
                return true;
            if (!SabotageButtonUnlockLogic.ColorAllowedForDevice(__instance, color))
            {
                __result = "";
                return false;
            }
            __result = SabotageButtonUnlockLogic.EvaluateMessage(__instance, index);
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_GameScene), "ShowInteractSabotageText")]
    internal static class SabotagePromptPatch
    {
        private static bool Prefix(UI_GameScene __instance)
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return true;

            SabotageButtonUnlockLogic.RefreshSabotagePrompt(__instance);
            return false;
        }
    }
}
