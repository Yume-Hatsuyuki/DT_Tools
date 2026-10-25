using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.CharacterMapPin
{
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
