using DT_Tools.Core;
using HarmonyLib;
using System.Reflection;

namespace DT_Tools.Patches.Fun.PhotoSend
{
    /// <summary>
    /// 聊天触发补丁：本地发送聊天消息时，若以 `!图片名`（或兼容的 `!photo 图片名`）开头
    /// 且能在图片文件夹里找到对应文件，则拦截（不发出去）并执行照片发送；
    /// `!` 开头但找不到文件的消息原样放行（不误伤普通感叹号聊天）。
    /// </summary>
    internal static class Patch
    {
        private const string LegacyTag = "!photo";

        /// <summary>PhotoManager._shots 私有字段（0.1.16b PhotoManager.cs:39，注入本地相册用）。</summary>
        internal static readonly FieldInfo PhotoShotsField =
            AccessTools.Field(typeof(PhotoManager), "_shots");

        [HarmonyPatch(typeof(VoiceManager), "SendChatMessage")]
        internal static class PhotoSendChatPatch
        {
            private static bool Prefix(VoiceManager __instance, ref string message)
            {
                if (!Engine.Enabled<PhotoSendFeature>())
                {
                    return true;
                }
                if (string.IsNullOrEmpty(message))
                {
                    return true;
                }

                string trimmed = message.TrimStart();
                // 半角 ! 或全角 ！ 开头才视为指令
                if (!trimmed.StartsWith("!", global::System.StringComparison.Ordinal) &&
                    !trimmed.StartsWith("！", global::System.StringComparison.Ordinal))
                {
                    return true;
                }

                bool legacy = trimmed.StartsWith(LegacyTag, global::System.StringComparison.OrdinalIgnoreCase);
                string name = legacy
                    ? trimmed.Substring(LegacyTag.Length).Trim()
                    : trimmed.Substring(1).Trim();

                if (name.Length == 0)
                {
                    return !legacy; // 空指令：旧格式拦截并提示，新格式放行
                }

                // 旧格式 !photo xxx 找不到也拦截报错；新格式 !xxx 找不到则当普通消息放行
                if (!legacy && !PhotoSendLogic.TryResolve(name, out _))
                {
                    return true;
                }

                string error = PhotoSendLogic.Execute(name);
                if (error == null)
                {
                    Log.Info<PhotoSendFeature>($"[PhotoSend] 已拦截聊天指令并发送照片：{name}");
                }
                else
                {
                    Log.Warn<PhotoSendFeature>("[PhotoSend] " + error);
                    try
                    {
                        __instance.SendChatMessage("[PhotoSend] " + error);
                    }
                    catch
                    {
                    }
                }
                return false; // 拦截原消息
            }
        }
    }
}
