using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.CharacterMapPin
{
    /// <summary>
    /// HUD LateUpdate 后置（白方强制显示他人 Pin，独立开关 ShowPinsForWhite）。
    /// LateUpdate 私有：0.1.15b UI_GameScene.cs:802；巡检本体在 Logic.SweepWhitePins。
    /// </summary>
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
