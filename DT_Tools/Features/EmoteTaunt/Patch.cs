using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Features.EmoteTaunt
{
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
                case 201: return EmoteTauntFeature.GaspLine;
                case 202: return EmoteTauntFeature.PanicLine;
                case 210: return EmoteTauntFeature.HeartLine;
                case 208: return EmoteTauntFeature.SmugLine;
                case 216: return EmoteTauntFeature.PunchLine;
                case 212: return EmoteTauntFeature.ThisOneLine;
                case 204: return EmoteTauntFeature.HmmLine;
                case 111: return EmoteTauntFeature.QuestionLine;
                case 206: return EmoteTauntFeature.AbsurdLine;
                case 112: return EmoteTauntFeature.SadLine;
                case 113: return EmoteTauntFeature.SorryLine;
                case 116: return EmoteTauntFeature.YahoLine;
                case 106: return EmoteTauntFeature.HelpLine;
                case 209: return EmoteTauntFeature.YikesLine;
                case 214: return EmoteTauntFeature.BlehLine;
                case 207: return EmoteTauntFeature.NopeLine;
                case 205: return EmoteTauntFeature.TooMuchLine;
                case 215: return EmoteTauntFeature.FightingLine;
                default: return null;
            }
        }
    }

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
