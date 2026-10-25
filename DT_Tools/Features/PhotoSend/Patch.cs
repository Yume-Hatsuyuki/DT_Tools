using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Features.PhotoSend
{
    internal static class Patch
    {
        internal static bool SendingCaption;

        [HarmonyPatch(typeof(VoiceManager), "SendChatMessage")]
        internal static class PhotoSendChatPatch
        {
            private static bool Prefix(VoiceManager __instance, string message)
            {
                if (!Engine.Enabled<PhotoSendFeature>())
                {
                    return true;
                }
                if (SendingCaption || string.IsNullOrEmpty(message))
                {
                    return true;
                }

                string trimmed = message.TrimStart();
                if (!trimmed.StartsWith("!", global::System.StringComparison.Ordinal))
                {
                    return true;
                }

                if (Managers.Game == null || !Managers.Game.IsAlive || Managers.Game.IsSpectator)
                {
                    return true;
                }

                string body = trimmed.Substring(1);
                ParseResult result = TryParseMarkdown(body, out string alt, out string argument);
                if (result == ParseResult.NotCommand)
                {
                    return true;
                }
                if (result != ParseResult.Ok)
                {
                    PhotoSendLogic.ReportChatError(__instance, Describe(result));
                    return false;
                }

                string error = PhotoSendLogic.Execute(__instance, alt, argument);
                if (error == null)
                {
                    Log.Info<PhotoSendFeature>($"已拦截聊天指令并发送照片：{argument}");
                }
                else
                {
                    Log.Warn<PhotoSendFeature>($"{error}");
                    PhotoSendLogic.ReportChatError(__instance, error);
                }
                return false;
            }

            private static string Describe(ParseResult result)
            {
                switch (result)
                {
                    case ParseResult.MissingParen: return "缺少 ]( 。" + PhotoSendLogic.Usage;
                    case ParseResult.MissingClose: return "缺少 ) 。" + PhotoSendLogic.Usage;
                    default: return $"路径不能为空。{PhotoSendLogic.Usage}";
                }
            }
        }

        private enum ParseResult
        {
            NotCommand,
            MissingParen,
            MissingClose,
            EmptyPath,
            Ok,
        }

        private static ParseResult TryParseMarkdown(string body, out string alt, out string argument)
        {
            alt = null;
            argument = null;

            if (body.Length == 0 || body[0] != '[')
            {
                return ParseResult.NotCommand;
            }

            int parenOpen = body.IndexOf("](", global::System.StringComparison.Ordinal);
            if (parenOpen < 0)
            {
                return ParseResult.MissingParen;
            }
            int close = body.IndexOf(')', parenOpen + 2);
            if (close < 0)
            {
                return ParseResult.MissingClose;
            }
            alt = body.Substring(1, parenOpen - 1).Trim();
            argument = body.Substring(parenOpen + 2, close - parenOpen - 2).Trim().Trim('"');
            if (argument.Length == 0)
            {
                return ParseResult.EmptyPath;
            }
            return ParseResult.Ok;
        }
    }
}
