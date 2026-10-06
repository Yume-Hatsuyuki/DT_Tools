using DT_Tools.Core;
using HarmonyLib;
using System.Reflection;

namespace DT_Tools.Patches.Fun.PhotoSend
{
    /// <summary>
    /// 聊天触发补丁：本地发送聊天消息时，若以 `!photo` 开头则拦截（不发出去）并执行照片发送。
    /// 其余消息原样放行。
    /// </summary>
    internal static class Patch
    {
        private const string Tag = "!photo";

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
                if (!trimmed.StartsWith(Tag, global::System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                string name = trimmed.Substring(Tag.Length).Trim();
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
