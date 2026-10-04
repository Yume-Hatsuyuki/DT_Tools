using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Patches.Experience.EmoteTaunt
{
    /// <summary>
    /// UseEmotion 后置：只对本地玩家（Managers.Player.MyPlayer）发出的表情触发喊话。
    /// 远端玩家表情经 S_USE_EMOTION → playerCache.UseEmotion 走同一方法，必须排除，避免冒名发言。
    /// 选用 Postfix 而非 Prefix 整替：与 EmoteHold 对 UseEmotion 的整替 Prefix 天然错开执行阶段，
    /// 两功能各自开关、互不干扰。
    /// 聊天走游戏自带全房广播：Managers.Voice.SendChatMessage
    /// （0.1.16b VoiceManager.cs:1239 → SendChatPacket(NormalChat) → C_CHAT_MESSAGE，房主转发全房），
    /// 因此接收方无需安装任何插件、也无需房主启用本功能。
    /// 台词逐表情配置于 Feature（留空=该表情不喊话），防刷屏冷却见 Cooldown。
    /// 庭审阶段：表情走 UI_TrialEvent.UseEmotion 而非 Player.UseEmotion（0.1.16b PacketHandler.cs:442），
    /// 由下方 EmoteTauntTrialPatch 单独钩住，与平时共用台词与冷却。
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.UseEmotion))]
    internal static class EmoteTauntPatch
    {
        internal static float _lastTauntAt;

        private static void Postfix(Player __instance, int emotionId)
        {
            if (!Engine.Enabled<EmoteTauntFeature>())
                return;

            if (Managers.Player == null || Managers.Player.MyPlayer != __instance)
                return;

            string line = GetLine(emotionId);
            if (string.IsNullOrEmpty(line))
                return;

            if (EmoteTauntFeature.Cooldown > 0f
                && Time.unscaledTime - _lastTauntAt < EmoteTauntFeature.Cooldown)
                return;

            _lastTauntAt = Time.unscaledTime;
            string message = string.IsNullOrEmpty(EmoteTauntFeature.Prefix)
                ? line
                : EmoteTauntFeature.Prefix + line;
            Managers.Voice?.SendChatMessage(message);
        }

        internal static string GetLine(int emotionId)
        {
            switch (emotionId)
            {
                case 201: return EmoteTauntFeature.GaspLine;     // GASP
                case 202: return EmoteTauntFeature.PanicLine;    // PANIC
                case 210: return EmoteTauntFeature.HeartLine;    // HEART
                case 208: return EmoteTauntFeature.SmugLine;     // SMUG
                case 216: return EmoteTauntFeature.PunchLine;    // PUNCH
                case 212: return EmoteTauntFeature.ThisOneLine;  // THISONE
                case 204: return EmoteTauntFeature.HmmLine;      // HMM
                case 111: return EmoteTauntFeature.QuestionLine; // QUESTION
                case 206: return EmoteTauntFeature.AbsurdLine;   // ABSURD
                case 112: return EmoteTauntFeature.SadLine;      // SAD
                case 113: return EmoteTauntFeature.SorryLine;    // SORRY
                case 116: return EmoteTauntFeature.YahoLine;     // YAHO
                case 106: return EmoteTauntFeature.HelpLine;     // HELP
                case 209: return EmoteTauntFeature.YikesLine;    // YIKES
                case 214: return EmoteTauntFeature.BlehLine;     // BLEH
                case 207: return EmoteTauntFeature.NopeLine;     // NOPE
                case 205: return EmoteTauntFeature.TooMuchLine;  // TOOMUCH
                case 215: return EmoteTauntFeature.FightingLine; // FIGHTING
                default: return null;
            }
        }
    }

    /// <summary>
    /// 庭审阶段钩子：庭审中表情经 UI_TrialEvent.UseEmotion(playerId, emotionId)（0.1.16b UI_TrialEvent.cs:1687），
    /// 本地玩家在庭审发表情也走此路（UI_EmotionSubItem 在 Trial 阶段不调 MyPlayer.UseEmotion）。
    /// 仅对本地玩家（MyPlayerID）的表情触发喊话，防冒名；台词与冷却与平时共享。
    /// </summary>
    [HarmonyPatch(typeof(UI_TrialEvent), nameof(UI_TrialEvent.UseEmotion))]
    internal static class EmoteTauntTrialPatch
    {
        private static void Postfix(int playerId, int emotionId)
        {
            if (!Engine.Enabled<EmoteTauntFeature>())
                return;
            if (Managers.Player == null || Managers.Player.MyPlayerID != playerId)
                return;

            string line = EmoteTauntPatch.GetLine(emotionId);
            if (string.IsNullOrEmpty(line))
                return;
            if (EmoteTauntFeature.Cooldown > 0f
                && Time.unscaledTime - EmoteTauntPatch._lastTauntAt < EmoteTauntFeature.Cooldown)
                return;

            EmoteTauntPatch._lastTauntAt = Time.unscaledTime;
            string message = string.IsNullOrEmpty(EmoteTauntFeature.Prefix)
                ? line
                : EmoteTauntFeature.Prefix + line;
            Managers.Voice?.SendChatMessage(message);
        }
    }
}
