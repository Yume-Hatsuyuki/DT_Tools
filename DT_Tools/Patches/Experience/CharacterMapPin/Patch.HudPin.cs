using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.CharacterMapPin
{
    /// <summary>
    /// HUD RefreshPlayerPin 后置：换上角色头像。私有方法，字符串定位：
    /// 0.1.15b UI_GameScene.cs:850。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameScene), "RefreshPlayerPin")]
    internal static class CharacterMapPinHudPinPatch
    {
        private static void Postfix(UI_GameScene __instance, Player player)
        {
            if (!Engine.Enabled<CharacterMapPinFeature>())
                return;

            if (player?.CharData == null)
                return;

            UI_MinimapSubItem pin = CharacterMapPinLogic.FindHudPin(__instance, player.PublicInfo.PlayerId);
            if (pin != null)
                CharacterMapPinUi.ApplyPin(pin, player, CharacterMapPinFeature.ReplaceBlackPin);
        }
    }
}
