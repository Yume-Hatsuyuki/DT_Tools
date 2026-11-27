// 作用：为角色头像 Pin挂载补丁拦截；不负责配置声明与状态存储。
using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.CharacterMapPin
{
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
