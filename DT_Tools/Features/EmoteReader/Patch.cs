using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Features.EmoteReader
{
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

        internal static string ReadLine(int emotionId)
        {
            switch (emotionId)
            {
                case 201: return EmoteReaderFeature.GaspLine;
                case 202: return EmoteReaderFeature.PanicLine;
                case 210: return EmoteReaderFeature.HeartLine;
                case 208: return EmoteReaderFeature.SmugLine;
                case 216: return EmoteReaderFeature.PunchLine;
                case 212: return EmoteReaderFeature.ThisOneLine;
                case 204: return EmoteReaderFeature.HmmLine;
                case 111: return EmoteReaderFeature.QuestionLine;
                case 206: return EmoteReaderFeature.AbsurdLine;
                case 112: return EmoteReaderFeature.SadLine;
                case 113: return EmoteReaderFeature.SorryLine;
                case 116: return EmoteReaderFeature.YahoLine;
                case 106: return EmoteReaderFeature.HelpLine;
                case 209: return EmoteReaderFeature.YikesLine;
                case 214: return EmoteReaderFeature.BlehLine;
                case 207: return EmoteReaderFeature.NopeLine;
                case 205: return EmoteReaderFeature.TooMuchLine;
                case 215: return EmoteReaderFeature.FightingLine;
                default: return null;
            }
        }
    }

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
