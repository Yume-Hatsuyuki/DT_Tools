using HarmonyLib;
using Protocol;

namespace DT_Tools.Patches
{
    /// <summary>
    /// 修改位置：System.Void UI_EmotionSubItem::UseEmotion()
    /// 表情包发送无CD
    /// 原逻辑：IsCooltime 为 true 时直接 return，发包后设为 true（锁定）
    /// 补丁逻辑：跳过 IsCooltime 判断，发包后强制重置为 false（不锁定）
    /// </summary>
    [HarmonyPatch(typeof(UI_EmotionSubItem), nameof(UI_EmotionSubItem.UseEmotion))]
    [PatchConfig("Enable_Patch_UseEmotionNoCD", "表情包发送无CD：跳过冷却时间判断，发包后立即解锁，可连续使用表情包。\n不受到 CosmeticSightingReporter 检测影响。", defaultEnabled: true, author: "梦初雪")]
    internal static class Patch_UseEmotionNoCD
    {
        [HarmonyPrefix]
        private static bool Prefix(UI_EmotionSubItem __instance)
        {
            // ── 合法性检查：不满足就静默退出，原方法也不执行 ────────────
            if (__instance.EmotionId <= 0)
                return true; // 让原方法自己处理这个 return

            UI_EmotionPanel ui_EmotionPanel =
                __instance.gameObject.FindComponentInParents<UI_EmotionPanel>();

            if (!Log.Assert(ui_EmotionPanel != null, "Not Find Parent : UI_EmotionPanel"))
                return true;

            if (!Managers.Game.IsAlive)
                return true;

            // ── IsCooltime 判断直接跳过（核心改动）─────────────────────
            // if (ui_EmotionPanel.IsCooltime) return;

            if (Managers.Player.MyPlayer != null &&
                Managers.Player.MyPlayer.State == EPlayerState.Possess)
                return true;

            if (!Traverse.Create(__instance).Method("CanUseEmotion").GetValue<bool>())
                return true;

            // ── 接管后续逻辑，发包后立即解锁 CD ─────────────────────────
            ui_EmotionPanel.IsCooltime = false; // 原来是 true，改为 false
            ui_EmotionPanel.EmotionClose();

            C_USE_EMOTION c_USE_EMOTION = new C_USE_EMOTION
            {
                EmoticonId = __instance.EmotionId
            };
            Managers.Network.GameServer.Send(c_USE_EMOTION);

            if (Managers.Game.State != EGameState.Trial)
            {
                Managers.Player.MyPlayer.UseEmotion(__instance.EmotionId);
            }

            return false; // 原方法已被我们完整替代，不再执行原方法
        }
    }
}