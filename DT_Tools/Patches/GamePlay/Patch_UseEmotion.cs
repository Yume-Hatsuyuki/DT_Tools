using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches.GamePlay
{
    /// <summary>
    /// <b>修改目标</b>：
    ///   UI_EmotionSubItem::UseEmotion()
    ///
    /// <b>原版效果</b>：
    ///   条件含 !uI_EmotionPanel.IsCooltime；通过后 IsCooltime = true，再发包与播放。
    ///
    /// <b>修改后效果</b>：
    ///   不检查 IsCooltime；发包前将 IsCooltime 设为 false。
    ///
    /// <b>修改方式</b>：
    ///   Prefix 按原版其余条件重写流程，省略冷却判断与锁定。
    /// </summary>
    [HarmonyPatch(typeof(UI_EmotionSubItem), nameof(UI_EmotionSubItem.UseEmotion))]
    [PatchConfig(
        "Enable_UseEmotion",
        "表情发送无冷却：可连续使用表情动作。",
        defaultEnabled: true,
        author: "梦初雪")]
    internal static class Patch_UseEmotion
    {
        [HarmonyPrefix]
        private static bool Prefix(UI_EmotionSubItem __instance)
        {
            if (__instance.EmotionId <= 0)
                return true;

            UI_EmotionPanel panel =
                __instance.gameObject.FindComponentInParents<UI_EmotionPanel>();

            if (!Log.Assert(panel != null, "Not Find Parent : UI_EmotionPanel"))
                return true;

            if (!Managers.Game.IsAlive)
                return true;

            // 原版：!panel.IsCooltime —— 此处不检查

            if (Managers.Player.MyPlayer != null &&
                Managers.Player.MyPlayer.State == EPlayerState.Possess)
                return true;

            if (!Traverse.Create(__instance).Method("CanUseEmotion").GetValue<bool>())
                return true;

            // 原版：panel.IsCooltime = true;
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
