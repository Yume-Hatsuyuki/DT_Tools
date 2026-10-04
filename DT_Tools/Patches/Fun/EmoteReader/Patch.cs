using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Patches.Fun.EmoteReader
{
    /// <summary>
    /// UseEmotion 后置：与 EmoteTaunt 共用同一方法但方向相反——这里只解读"别人"的表情
    /// （Managers.Player.MyPlayer == __instance 时跳过，不自我解说）。
    /// 表情 id→解读台词映射与 EmoteTaunt 的 18 个表情一一对应（0.1.16b Emotions.cs），
    /// 台词逐表情配置于 Feature（配置页可直接修改，留空=该表情不解读）。
    /// 广播描述的是"对方的行为"（XX 的解读），不是冒充对方发言，无冒名风险。
    /// 庭审阶段：表情走 UI_TrialEvent.UseEmotion 而非 Player.UseEmotion（0.1.16b PacketHandler.cs:442），
    /// 由下方 EmoteReaderTrialPatch 单独钩住，与平时共用台词与冷却。
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.UseEmotion))]
    internal static class EmoteReaderPatch
    {
        internal static float _lastReadAt;

        private static void Postfix(Player __instance, int emotionId)
        {
            if (!Engine.Enabled<EmoteReaderFeature>())
                return;
            if (Managers.Player == null || Managers.Player.MyPlayer == __instance)
                return;
            if (EmoteReaderFeature.Cooldown > 0f
                && Time.unscaledTime - _lastReadAt < EmoteReaderFeature.Cooldown)
                return;

            string reading = ReadLine(emotionId);
            if (string.IsNullOrEmpty(reading))
                return;

            string name = string.IsNullOrEmpty(__instance.DisplayName)
                ? "玩家" + __instance.Name
                : __instance.DisplayName;
            _lastReadAt = Time.unscaledTime;
            Managers.Voice?.SendChatMessage($"{name} {reading}");
        }

        /// <summary>emotionId → 解读台词（读配置）；未知名表情返回 null。</summary>
        internal static string ReadLine(int emotionId)
        {
            switch (emotionId)
            {
                case 201: return EmoteReaderFeature.GaspLine;     // GASP
                case 202: return EmoteReaderFeature.PanicLine;    // PANIC
                case 210: return EmoteReaderFeature.HeartLine;    // HEART
                case 208: return EmoteReaderFeature.SmugLine;     // SMUG
                case 216: return EmoteReaderFeature.PunchLine;    // PUNCH
                case 212: return EmoteReaderFeature.ThisOneLine;  // THISONE
                case 204: return EmoteReaderFeature.HmmLine;      // HMM
                case 111: return EmoteReaderFeature.QuestionLine; // QUESTION
                case 206: return EmoteReaderFeature.AbsurdLine;   // ABSURD
                case 112: return EmoteReaderFeature.SadLine;      // SAD
                case 113: return EmoteReaderFeature.SorryLine;    // SORRY
                case 116: return EmoteReaderFeature.YahoLine;     // YAHO
                case 106: return EmoteReaderFeature.HelpLine;     // HELP
                case 209: return EmoteReaderFeature.YikesLine;    // YIKES
                case 214: return EmoteReaderFeature.BlehLine;     // BLEH
                case 207: return EmoteReaderFeature.NopeLine;     // NOPE
                case 205: return EmoteReaderFeature.TooMuchLine;  // TOOMUCH
                case 215: return EmoteReaderFeature.FightingLine; // FIGHTING
                default: return null;
            }
        }
    }

    /// <summary>
    /// 庭审阶段钩子：庭审中表情经 UI_TrialEvent.UseEmotion(playerId, emotionId)（0.1.16b UI_TrialEvent.cs:1687），
    /// 只解读他人（playerId != MyPlayerID），玩家名从 PlayerCache 取；台词与冷却与平时共享。
    /// </summary>
    [HarmonyPatch(typeof(UI_TrialEvent), nameof(UI_TrialEvent.UseEmotion))]
    internal static class EmoteReaderTrialPatch
    {
        private static void Postfix(int playerId, int emotionId)
        {
            if (!Engine.Enabled<EmoteReaderFeature>())
                return;
            if (Managers.Player == null || Managers.Player.MyPlayerID == playerId)
                return;
            if (EmoteReaderFeature.Cooldown > 0f
                && Time.unscaledTime - EmoteReaderPatch._lastReadAt < EmoteReaderFeature.Cooldown)
                return;

            string reading = EmoteReaderPatch.ReadLine(emotionId);
            if (string.IsNullOrEmpty(reading))
                return;

            Player target = Managers.Player.GetPlayerCache(playerId);
            string name = (target == null || string.IsNullOrEmpty(target.DisplayName))
                ? "玩家" + (target == null ? "" + playerId : target.Name)
                : target.DisplayName;
            EmoteReaderPatch._lastReadAt = Time.unscaledTime;
            Managers.Voice?.SendChatMessage($"{name} {reading}");
        }
    }
}
