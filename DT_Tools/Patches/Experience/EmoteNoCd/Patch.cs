using DT_Tools.Core;
using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.Experience.EmoteNoCd
{
    /// <summary>
    /// UseEmotion 整替：复刻 0.1.15b UI_EmotionSubItem.cs:87 的判断链，
    /// 仅去掉 IsCooltime 门闩与冷却置位（并恒置 IsCooltime = false 清掉残留冷却）。
    /// CanUseEmotion 为 private（0.1.15b UI_EmotionSubItem.cs:110），经 Traverse 调用。
    /// 原版经全局 Log.Assert 报父面板缺失（0.1.15b UI_EmotionSubItem.cs:94），
    /// 按日志规范改走本插件 Log 门面。
    /// </summary>
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
                Log.Error<EmoteNoCdFeature>("Not Find Parent : UI_EmotionPanel");

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
