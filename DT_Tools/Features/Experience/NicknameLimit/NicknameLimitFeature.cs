using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using DT_Tools.Core;

namespace DT_Tools.Features.Experience
{
    /// <summary>
    /// 取消主页昵称字数上限（权重 CJK×2+字母数字 ≤12）。
    /// 源码侧无 characterLimit；若 Prefab 仍设了硬上限，一并抬高。
    /// 门禁点：OnValidateNickname / EnsureNicknameOrShowError / 各进房入口读 _isNickNameValid。
    /// </summary>
    [HarmonyPatch]
    [PatchFeature(
        section: "NicknameNoLimit",
        description: "取消主页昵称字数上限：可超过原版权重 12；空名与非法字符仍不可用。",
        defaultEnabled: false,
        side: FeatureSide.Client,
        author: "梦初雪")]
    internal static class NicknameLimitFeature
    {
        /// <summary>TMP 硬上限；部分版本 0 表示无限制，为兼容统一抬到较大值。</summary>
        private const int SoftCap = 64;

        // 与原版 OnValidateNickname 白名单一致（分析文档 §2.3 / §6）
        private static readonly Regex Other = new Regex(
            "[^a-zA-Z0-9\\u3131-\\u318E\\uAC00-\\uD7A3\\u3040-\\u309F\\u30A0-\\u30FF\\u31F0-\\u31FF\\u4E00-\\u9FFF]",
            RegexOptions.Compiled);

        public static void OnEnabled() => RefreshAllLobbyScenes("OnEnabled");

        private static bool IsNameAcceptable(string name, out string failTip)
        {
            failTip = null;
            if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name))
            {
                try { failTip = Managers.GetText("LobbyNicknameEmpty"); }
                catch { failTip = ""; }
                return false;
            }
            // 非法字符（空格、emoji、下划线等）仍拦截，与原版一致
            if (name.Any(c => Other.IsMatch(c.ToString())))
            {
                try { failTip = Managers.GetText("LobbyNicknameInvalidChar"); }
                catch { failTip = ""; }
                return false;
            }
            // 故意不做 num*2+num2 > 12
            return true;
        }

        private static TMP_InputField GetNicknameField(UI_LobbyScene scene)
        {
            try
            {
                return Traverse.Create(scene)
                    .Method("GetInputField", new object[] { 1 })
                    .GetValue<TMP_InputField>();
            }
            catch
            {
                return null;
            }
        }

        private static void LiftInputHardLimit(TMP_InputField field)
        {
            if (field == null) return;
            // Prefab 可能设 12；源码未写 characterLimit（分析 §2.4b）
            if (field.characterLimit != SoftCap)
            {
                int old = field.characterLimit;
                field.characterLimit = SoftCap;
                if (old != SoftCap)
                {
                    FeatureLogRegistry.Info("NicknameNoLimit",
                        $"昵称输入框 characterLimit {old} → {SoftCap}");
                }
            }
        }

        private static void ApplyValidUi(UI_LobbyScene scene, bool valid, string tip)
        {
            var t = Traverse.Create(scene);
            t.Field("_isNickNameValid").SetValue(valid);

            // 直接清/写提示，避免仍显示「昵称太长了」
            try
            {
                object tipObj = t.Method("GetText", new object[] { 15 }).GetValue();
                if (tipObj is TMP_Text tipText)
                    tipText.text = tip ?? "";

                Color c = valid ? Color.white : new Color(0.25f, 0.25f, 0.25f, 1f);
                foreach (int id in new[] { 16, 17, 18 })
                {
                    object o = t.Method("GetText", new object[] { id }).GetValue();
                    if (o is TMP_Text tmp)
                        tmp.color = c;
                }
            }
            catch
            {
                // ignore
            }
        }

        private static void RefreshScene(UI_LobbyScene scene, string reason)
        {
            if (scene == null) return;
            var field = GetNicknameField(scene);
            LiftInputHardLimit(field);

            string name = field != null ? field.text : Managers.Player?.MyPlayerName;
            bool ok = IsNameAcceptable(name, out string tip);
            ApplyValidUi(scene, ok, tip ?? "");

            // 合法时同步 MyPlayerName（与原版写入时机一致，但仅在可接受时）
            if (ok && name != null)
                Managers.Player.MyPlayerName = name;

            FeatureLogRegistry.Info("NicknameNoLimit",
                $"[{reason}] ok={ok} len={name?.Length ?? 0} limit={field?.characterLimit ?? -1}");
        }

        private static void RefreshAllLobbyScenes(string reason)
        {
            try
            {
                var scenes = Object.FindObjectsByType<UI_LobbyScene>(FindObjectsSortMode.None);
                if (scenes == null || scenes.Length == 0)
                {
                    FeatureLogRegistry.Info("NicknameNoLimit", $"[{reason}] 当前无 UI_LobbyScene");
                    return;
                }
                foreach (var scene in scenes)
                    RefreshScene(scene, reason);
            }
            catch (global::System.Exception ex)
            {
                FeatureLogRegistry.Error("NicknameNoLimit", "扫描大厅失败: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(UI_LobbyScene), nameof(UI_LobbyScene.Init))]
        [HarmonyPostfix]
        private static void PostfixInit(UI_LobbyScene __instance, bool __result)
        {
            if (!__result || !FeatureGate.Enabled(typeof(NicknameLimitFeature)))
                return;
            RefreshScene(__instance, "Init");
        }

        [HarmonyPatch(typeof(UI_LobbyScene), "StartLobbySection")]
        [HarmonyPostfix]
        private static void PostfixStartLobby(UI_LobbyScene __instance)
        {
            if (!FeatureGate.Enabled(typeof(NicknameLimitFeature)))
                return;
            // StartLobbySection 会再调 OnValidateNickname；之后再抬限并清红字
            RefreshScene(__instance, "StartLobbySection");
        }

        [HarmonyPatch(typeof(UI_LobbyScene), "OnValidateNickname")]
        [HarmonyPrefix]
        private static bool PrefixValidate(UI_LobbyScene __instance, string name)
        {
            if (!FeatureGate.Enabled(typeof(NicknameLimitFeature)))
                return true;

            LiftInputHardLimit(GetNicknameField(__instance));

            // 与原版一致：先写入（分析 §3）；我们仅放宽长度，非法仍可被写进 Prefs，
            // 但门禁 _isNickNameValid 会挡住进房。
            Managers.Player.MyPlayerName = name;
            var t = Traverse.Create(__instance);
            string key = t.Field("_nicknameKey").GetValue<string>();
            if (key != null)
                PlayerPrefs.SetString(key, name);

            bool ok = IsNameAcceptable(name, out string tip);
            ApplyValidUi(__instance, ok, tip ?? "");
            FeatureLogRegistry.Info("NicknameNoLimit",
                ok ? $"校验通过 len={name?.Length ?? 0}" : $"校验失败: {tip}");
            return false;
        }

        [HarmonyPatch(typeof(UI_LobbyScene), "EnsureNicknameOrShowError")]
        [HarmonyPrefix]
        private static bool PrefixEnsure(UI_LobbyScene __instance, ref bool __result)
        {
            if (!FeatureGate.Enabled(typeof(NicknameLimitFeature)))
                return true;

            LiftInputHardLimit(GetNicknameField(__instance));
            string name = GetNicknameField(__instance)?.text ?? Managers.Player?.MyPlayerName;
            bool ok = IsNameAcceptable(name, out string tip);
            ApplyValidUi(__instance, ok, tip ?? "");
            __result = ok;
            return false;
        }
    }
}
