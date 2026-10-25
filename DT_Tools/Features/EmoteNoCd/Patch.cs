using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Features.EmoteNoCd
{
    [HarmonyPatch(typeof(UI_EmotionSubItem), nameof(UI_EmotionSubItem.UseEmotion))]
    internal static class EmoteNoCdPatch
    {
        private static bool Prefix(UI_EmotionSubItem __instance)
        {
            if (!Engine.Enabled<EmoteNoCdFeature>())
                return true;

            if (__instance.EmotionId <= 0)
                return false;

            UI_EmotionPanel panel =
                __instance.gameObject.FindComponentInParents<UI_EmotionPanel>();
            if (panel == null)
                Log.Error<EmoteNoCdFeature>("父面板缺失：UI_EmotionPanel");

            bool canUse =
                panel != null
                && Managers.Game.IsAlive
                && (Managers.Player.MyPlayer == null
                    || Managers.Player.MyPlayer.State != EPlayerState.Possess)
                && Traverse.Create(__instance).Method("CanUseEmotion").GetValue<bool>();

            if (!canUse)
                return false;

            panel.IsCooltime = false;
            panel.EmotionClose();
            Managers.Network.GameServer.Send(new C_USE_EMOTION
            {
                EmoticonId = __instance.EmotionId
            });
            if (Managers.Game.State != EGameState.Trial)
                Managers.Player.MyPlayer.UseEmotion(__instance.EmotionId);

            return false;
        }
    }
}
