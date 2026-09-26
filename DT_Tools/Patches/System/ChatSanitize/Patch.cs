using DT_Tools.Core;
using HarmonyLib;
using Server.Game;

namespace DT_Tools.Patches.System.ChatSanitize
{
    /// <summary>
    /// SanitizeChat 整替：原版硬编码 100 截断（0.1.15b Server.Game/GameRoom.cs:2524，
    /// Util.NeutralizeRichText 在 0.1.15b Util.cs:172），改为读配置上限并保留代理对完整性。
    /// </summary>
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.SanitizeChat))]
    internal static class ChatSanitizePatch
    {
        private static bool Prefix(string raw, ref string __result)
        {
            if (!Engine.Enabled<ChatSanitizeFeature>())
                return true;

            string text = raw ?? "";
            int max = ChatSanitizeFeature.MaxLength;
            if (text.Length > max)
            {
                int n = max;
                // 不截断在代理对中间（emoji 等增补字符）
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
