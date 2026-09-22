using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 放宽聊天字数：提高输入框上限，并放宽房主侧 SanitizeChat。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "ChatNoTruncate",
        description: "取消聊天字数截断：输入框与房主侧过滤使用更大上限（默认 1000，可改 MaxLength）。",
        defaultEnabled: false,
        side: FeatureSide.Both,
        author: "梦初雪")]
    internal static class ChatLimitFeature
    {
        [ConfigField(1000, "聊天最大字数（输入框与房主 SanitizeChat 共用，范围 100～8000）。")]
        public static ConfigEntry<int> MaxLength;

        private static int Cap =>
            MaxLength != null ? Mathf.Clamp(MaxLength.Value, 100, 8000) : 1000;

        public static void OnEnabled() => ApplyToExistingUi();

        private static void RaiseLimit(TMP_InputField field)
        {
            if (field == null) return;
            int cap = Cap;
            if (field.characterLimit > 0 && field.characterLimit <= 100)
                field.characterLimit = cap;
            else if (field.characterLimit > 0 && field.characterLimit < cap)
                field.characterLimit = cap;
        }

        private static void ApplyToExistingUi()
        {
            try
            {
                var fields = Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None);
                int n = 0;
                foreach (var f in fields)
                {
                    if (f == null) continue;
                    if (f.characterLimit > 0 && f.characterLimit <= 100)
                    {
                        RaiseLimit(f);
                        n++;
                    }
                }
                FeatureLogRegistry.Info("ChatNoTruncate", $"已抬高 {n} 个输入框上限 → {Cap}");
            }
            catch (global::System.Exception ex)
            {
                FeatureLogRegistry.Error("ChatNoTruncate", "扫描输入框失败: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(UI_GameTablet), nameof(UI_GameTablet.Init))]
        [HarmonyPostfix]
        private static void PostfixTabletInit(UI_GameTablet __instance)
        {
            if (!FeatureGate.Enabled(typeof(ChatLimitFeature)))
                return;
            try
            {
                var field = Traverse.Create(__instance)
                    .Method("GetInputField", new object[] { 0 }).GetValue<TMP_InputField>();
                RaiseLimit(field);
            }
            catch { /* */ }
        }

        [HarmonyPatch(typeof(UI_DirectChat), nameof(UI_DirectChat.Init))]
        [HarmonyPostfix]
        private static void PostfixDirectChat(UI_DirectChat __instance)
        {
            if (!FeatureGate.Enabled(typeof(ChatLimitFeature)))
                return;
            RaiseLimit(Traverse.Create(__instance).Field("_input").GetValue<TMP_InputField>());
        }

        [HarmonyPatch(typeof(UI_ChatDevicePopup), nameof(UI_ChatDevicePopup.Init))]
        [HarmonyPostfix]
        private static void PostfixChatDevice(UI_ChatDevicePopup __instance)
        {
            if (!FeatureGate.Enabled(typeof(ChatLimitFeature)))
                return;
            RaiseLimit(Traverse.Create(__instance).Field("_input").GetValue<TMP_InputField>());
        }

        /// <summary>任意 TMP 输入框启用时，若仍是原版 100 上限则抬高。</summary>
        [HarmonyPatch(typeof(TMP_InputField), "OnEnable")]
        [HarmonyPostfix]
        private static void PostfixTmpOnEnable(TMP_InputField __instance)
        {
            if (!FeatureGate.Enabled(typeof(ChatLimitFeature)))
                return;
            RaiseLimit(__instance);
        }

        [HarmonyPatch(typeof(TMP_InputField), nameof(TMP_InputField.ActivateInputField))]
        [HarmonyPostfix]
        private static void PostfixActivate(TMP_InputField __instance)
        {
            if (!FeatureGate.Enabled(typeof(ChatLimitFeature)))
                return;
            RaiseLimit(__instance);
        }

        [HarmonyPatch(typeof(Server.Game.GameRoom), nameof(Server.Game.GameRoom.SanitizeChat))]
        [HarmonyPrefix]
        private static bool PrefixSanitize(string raw, ref string __result)
        {
            if (!FeatureGate.Enabled(typeof(ChatLimitFeature)))
                return true;

            string text = raw ?? "";
            int max = Cap;
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
