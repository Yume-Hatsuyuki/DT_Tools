using System;
using DT_Tools.Core;
using HarmonyLib;
using Server.Game;

namespace DT_Tools.Features.ChatSanitize
{
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.SanitizeChat))]
    internal static class ChatSanitizePatch
    {
        private static bool Prefix(string raw, ref string __result)
        {
            if (!Engine.Enabled<ChatSanitizeFeature>())
                return true;

            string text = raw ?? "";
            int max = Math.Max(100, ChatSanitizeFeature.MaxLength);
            if (text.Length > max)
            {
                int n = max;
                if (n > 0 && char.IsHighSurrogate(text[n - 1]))
                    n--;
                if (n < text.Length)
                    text = text.Substring(0, n);
            }
            __result = Util.NeutralizeRichText(text);
            return false;
        }
    }
}
