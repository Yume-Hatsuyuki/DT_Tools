using DT_Tools.Core;
using HarmonyLib;

namespace DT_Tools.Patches.Fun.PhotoSend
{
    /// <summary>
    /// 聊天触发补丁：`![文字](路径)` 直发照片（严格 markdown 括号对，半角）。
    /// 文字非空时先发一条同内容的普通聊天消息再发照片；文字可空（`![](路径)`），路径不允许为空。
    /// 大厅/观战/死亡（原版照片入口不可用的状态）与其余 `!` 开头消息一律放行不接管。
    /// </summary>
    internal static class Patch
    {
        /// <summary>补丁自己发送文字说明期间的再入闸：文字若以 ! 开头，防止被本补丁再次解析成指令。</summary>
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
                    return true; // 严格 markdown：只认半角 !
                }

                // 大厅/观战/死亡时原版照片入口不可用（与 0.1.16b UI_GameTablet.RefreshPhotoButton 同条件），
                // 此时完全不接管，避免在大厅等场景吃掉消息
                if (Managers.Game == null || !Managers.Game.IsAlive || Managers.Game.IsSpectator)
                {
                    return true;
                }

                string body = trimmed.Substring(1);
                ParseResult result = TryParseMarkdown(body, out string alt, out string argument);
                if (result == ParseResult.NotCommand)
                {
                    return true; // 非 [ 开头：普通聊天（旧式 !photo 等一并放行）
                }
                if (result != ParseResult.Ok)
                {
                    PhotoSendLogic.ReportChatError(__instance, Describe(result));
                    return false;
                }

                string error = PhotoSendLogic.Execute(__instance, alt, argument);
                if (error == null)
                {
                    Log.Info<PhotoSendFeature>($"[PhotoSend] 已拦截聊天指令并发送照片：{argument}");
                }
                else
                {
                    Log.Warn<PhotoSendFeature>("[PhotoSend] " + error);
                    PhotoSendLogic.ReportChatError(__instance, error);
                }
                return false; // 拦截原消息
            }

            private static string Describe(ParseResult result)
            {
                switch (result)
                {
                    case ParseResult.MissingParen: return "缺少 ]( 。" + PhotoSendLogic.Usage;
                    case ParseResult.MissingClose: return "缺少 ) 。" + PhotoSendLogic.Usage;
                    default: return "路径不能为空。" + PhotoSendLogic.Usage;
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

        /// <summary>解析 markdown 图片语法：`[` 文字 `](` 路径 `)`。文字可空（Trim 后），路径不允许为空。</summary>
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
