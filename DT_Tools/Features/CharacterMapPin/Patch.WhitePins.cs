using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.CharacterMapPin
{
    [HarmonyPatch(typeof(UI_GameScene), "LateUpdate")]
    internal static class CharacterMapPinWhitePinsPatch
    {
        private static void Postfix(UI_GameScene __instance)
        {
            if (!Engine.Enabled<CharacterMapPinFeature>())
                return;

            if (!CharacterMapPinFeature.ShowPinsForWhite)
                return;

            CharacterMapPinLogic.SweepWhitePins(__instance);
        }
    }
}
