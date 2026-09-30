using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.CharacterMapPin
{
    /// <summary>
    /// 平板 RefreshPlayerPin 后置：换上角色头像。私有方法，字符串定位：
    /// 0.1.15b UI_GameTablet.cs:1211。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameTablet), "RefreshPlayerPin")]
    internal static class CharacterMapPinTabletPinPatch
    {
        private static void Postfix(UI_GameTablet __instance, Player player)
        {
            if (!Engine.Enabled<CharacterMapPinFeature>())
                return;

            if (player?.CharData == null)
                return;

            UI_MinimapSubItem pin = CharacterMapPinLogic.FindTabletPin(__instance, player.PublicInfo.PlayerId);
            if (pin != null)
                CharacterMapPinUi.ApplyPin(pin, player, CharacterMapPinFeature.ReplaceBlackPin);
        }
    }
}
