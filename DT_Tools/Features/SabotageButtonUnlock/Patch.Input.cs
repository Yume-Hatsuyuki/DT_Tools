using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.SabotageButtonUnlock
{
    [HarmonyPatch(typeof(MyPlayer), "InputInteract")]
    internal static class SabotageInputPatch
    {
        private static bool Prefix(MyPlayer __instance)
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return true;
            if (!KeyBindings.Down(Define.EInputAction.Special))
                return true;
            if (SabotageButtonUnlockLogic.TryDispatchExtended(__instance))
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(MyPlayer), "UpdateCarry")]
    internal static class SabotageCarryInputPatch
    {
        private static bool Prefix(MyPlayer __instance)
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return true;
            if (!KeyBindings.Down(Define.EInputAction.Special))
                return true;
            if (SabotageButtonUnlockLogic.TryDispatchExtended(__instance))
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(UI_GameScene), "OnClickKillButton")]
    internal static class SabotageKillButtonPatch
    {
        private static bool Prefix()
        {
            if (!Engine.Enabled<SabotageButtonUnlockFeature>())
                return true;
            var my = Managers.Player.MyPlayer;
            if (my == null)
                return true;
            if (SabotageButtonUnlockLogic.TryDispatchExtended(my))
                return false;
            return true;
        }
    }
}
