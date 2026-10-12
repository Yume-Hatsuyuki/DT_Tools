using TMPro;
using UnityEngine;

namespace DT_Tools.Features.ChatLimit
{
    /// 输入框上限抬升与全场景扫描。原版各聊天输入框硬编码 characterLimit = 100
    /// （0.1.16b UI_GameTablet.cs:871（待核对）、UI_DirectChat.cs:88、UI_ChatDevicePopup.cs:95）。
    /// 房主侧发言过滤（原 GameRoom.SanitizeChat）已拆分至 System/ChatSanitize。
    internal static class ChatLimitLogic
    {
        /// 原版三个聊天框的硬编码上限（抬限只认这个精确值）。
        public const int VanillaChatLimit = 100;

        private const int MinLimit = 100;
        private const int MaxLimit = 8000;

        public static int Cap => Mathf.Clamp(ChatLimitFeature.MaxLength, MinLimit, MaxLimit);

        /// 抬限只认原版聊天框的精确值 100：不误伤 Prefab 内嵌的其他小上限
        /// （如昵称输入框，归 NicknameLimit 管），也不覆盖更大的内嵌上限
        /// （如 UI_BugReport 的 2000，0.1.17a UI_BugReport.cs:5）——否则全局
        /// TmpOnEnable/TmpActivate 兜底会波及所有输入框并与 NicknameLimit 互相拉扯。
        public static void RaiseLimit(TMP_InputField field)
        {
            if (field == null)
                return;
            if (field.characterLimit == VanillaChatLimit)
                field.characterLimit = Cap;
        }

        /// Enabled 热开启时对当前已存在的 TMP 输入框补一次扫描。
        public static void ApplyToExistingUi()
        {
            try
            {
                var fields = Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None);
                int n = 0;
                foreach (var f in fields)
                {
                    if (f == null || f.characterLimit != VanillaChatLimit)
                        continue;
                    RaiseLimit(f);
                    n++;
                }
                Log.Info<ChatLimitFeature>($"已抬高 {n} 个输入框上限 → {Cap}");
            }
            catch (global::System.Exception ex)
            {
                Log.Error<ChatLimitFeature>(ex, "扫描输入框失败");
            }
        }
    }
}
