using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Experience.CharacterMapPin
{
    /// <summary>
    /// 平板 RefreshBlackPin(int) 后置：已识破黑幕的黑点也换黑色角色头像。
    /// 公共方法（nameof 定位）：0.1.15b UI_GameTablet.cs:1229。
    /// </summary>
    [HarmonyPatch(typeof(UI_GameTablet), nameof(UI_GameTablet.RefreshBlackPin))]
    internal static class CharacterMapPinTabletBlackPinPatch
    {
        private static void Postfix(UI_GameTablet __instance, int id)
        {
            if (!Engine.Enabled<CharacterMapPinFeature>())
                return;

            if (!CharacterMapPinFeature.ReplaceBlackPin)
                return;

            Player player = Managers.Player.GetPlayerCache(id);
            if (player?.CharData == null)
                return;

            UI_MinimapSubItem pin = CharacterMapPinLogic.FindTabletPin(__instance, id);
            if (pin != null)
                CharacterMapPinUi.ApplyPin(pin, player, replaceBlack: true);
        }
    }
}
