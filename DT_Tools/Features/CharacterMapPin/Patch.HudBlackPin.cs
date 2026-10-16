using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.CharacterMapPin
{
    /// <summary>
    /// HUD RefreshBlackPin(int) 后置：已识破黑幕的黑点也换黑色角色头像。
    /// </summary>
    /// <remarks>
    /// 公共方法（nameof 定位）：0.1.18c UI_GameScene.cs。
    /// </remarks>
    [HarmonyPatch(typeof(UI_GameScene), nameof(UI_GameScene.RefreshBlackPin))]
    internal static class CharacterMapPinHudBlackPinPatch
    {
        private static void Postfix(UI_GameScene __instance, int id)
        {
            if (!Engine.Enabled<CharacterMapPinFeature>())
                return;

            if (!CharacterMapPinFeature.ReplaceBlackPin)
                return;

            Player player = Managers.Player.GetPlayerCache(id);
            if (player?.CharData == null)
                return;

            UI_MinimapSubItem pin = CharacterMapPinLogic.FindHudPin(__instance, id);
            if (pin != null)
                CharacterMapPinUi.ApplyPin(pin, player, replaceBlack: true);
        }
    }
}
