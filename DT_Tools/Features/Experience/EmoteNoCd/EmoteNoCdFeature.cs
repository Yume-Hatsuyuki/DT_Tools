using HarmonyLib;
using Protocol;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 表情无冷却。目标：UI_EmotionSubItem.UseEmotion — 去掉 IsCooltime 门闩，且不进入冷却。
    /// </summary>
    [HarmonyPatch(typeof(UI_EmotionSubItem), nameof(UI_EmotionSubItem.UseEmotion))]
    [PatchFeature(
        section: "UseEmotion",
        description: "表情发送无冷却：可连续使用表情动作。",
        defaultEnabled: true,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class EmoteNoCdFeature
    {
        [HarmonyPrefix]
        private static bool Prefix(UI_EmotionSubItem __instance)
        {
            // 对齐 0.1.14b UI_EmotionSubItem.UseEmotion：仅移除冷却判断与冷却置位。
            if (__instance.EmotionId <= 0)
                return false;

            UI_EmotionPanel panel =
                __instance.gameObject.FindComponentInParents<UI_EmotionPanel>();

            bool canUse =
                Log.Assert(panel != null, "Not Find Parent : UI_EmotionPanel")
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
