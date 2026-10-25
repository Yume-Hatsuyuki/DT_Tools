using TMPro;
using UnityEngine;

namespace DT_Tools.Features.ChatLimit
{
    internal static class ChatLimitLogic
    {
        public const int VanillaChatLimit = 100;

        private const int MinLimit = 100;
        private const int MaxLimit = 8000;

        public static int Cap => Mathf.Clamp(ChatLimitFeature.MaxLength, MinLimit, MaxLimit);

        public static void RaiseLimit(TMP_InputField field)
        {
            if (field == null)
                return;
            if (field.characterLimit == VanillaChatLimit)
                field.characterLimit = Cap;
        }

        public static void RestoreExistingUi()
        {
            try
            {
                var fields = Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None);
                int n = 0;
                foreach (var f in fields)
                {
                    if (f == null || f.characterLimit != Cap)
                    {
                        continue;
                    }
                    f.characterLimit = VanillaChatLimit;
                    n++;
                }
                Log.Info<ChatLimitFeature>($"已还原 {n} 个输入框上限 → {VanillaChatLimit}");
            }
            catch (global::System.Exception ex)
            {
                Log.Error<ChatLimitFeature>(ex, "还原输入框上限失败");
            }
        }

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
