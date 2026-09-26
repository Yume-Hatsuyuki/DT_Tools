using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DT_Tools.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace DT_Tools.Patches.System.NicknameLimit
{
    /// <summary>
    /// 昵称校验与大厅 UI 刷新：与原版 OnValidateNickname（0.1.15b UI_LobbyScene.cs:1559）
    /// 同一套字符白名单，仅去掉「权重 > 12」这一档；门禁 _isNickNameValid 的写法保持一致。
    /// </summary>
    internal static class NicknameLimitLogic
    {
        /// <summary>TMP 硬上限；部分版本 0 表示无限制，为兼容统一抬到较大值。</summary>
        public const int SoftCap = 64;

        // ── UI 元素索引（UI_Base 的 protected GetText/GetInputField，0.1.15b UI_Base.cs:98/:103，
        //    Traverse 定位）──
        /// <summary>昵称错误提示文本（原版 :500 清空、:1590 写 LobbyNicknameTooLong）。</summary>
        private const int TextErrorTip = 15;
        /// <summary>三个大厅按钮文字，校验结果以灰/白着色（原版 :703-709 的着色与文案）。</summary>
        private const int TextCreateGame = 16;
        private const int TextFindGame = 17;
        private const int TextEnterGame = 18;

        private static readonly int[] ButtonTextIds = { TextCreateGame, TextFindGame, TextEnterGame };

        // 与原版 OnValidateNickname 白名单一致（0.1.15b UI_LobbyScene.cs:1559 的正则）
        private static readonly Regex Other = new Regex(
            "[^a-zA-Z0-9\\u3131-\\u318E\\uAC00-\\uD7A3\\u3040-\\u309F\\u30A0-\\u30FF\\u31F0-\\u31FF\\u4E00-\\u9FFF]",
            RegexOptions.Compiled);

        public static bool IsNameAcceptable(string name, out string failTip)
        {
            failTip = null;
            if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name))
            {
                try { failTip = Managers.GetText("LobbyNicknameEmpty"); }
                catch { failTip = ""; }
                return false;
            }
            // 非法字符（空格、emoji、下划线等）默认拦截，与原版一致；
            // AllowAnyChar 开启时本维度整体放开（权重档始终移除，见下）
            if (!NicknameLimitFeature.AllowAnyChar && name.Any(c => Other.IsMatch(c.ToString())))
            {
                try { failTip = Managers.GetText("LobbyNicknameInvalidChar"); }
                catch { failTip = ""; }
                return false;
            }
            // 故意不做 num*2+num2 > 12（原版 :1585 一档被移除）
            return true;
        }

        private static bool _allowAnyCharWired;

        /// <summary>
        /// 订阅 AllowAnyChar 配置变更：热改即重校验当前大厅场景（同 OnEnabled/OnDisabled 刷新），
        /// 保证门禁与按钮着色立即跟随开关。
        /// </summary>
        public static void WireAllowAnyCharChanged()
        {
            if (_allowAnyCharWired)
                return;
            if (!Engine.Config.TryGetEntry<bool>(
                    Engine.SectionOf<NicknameLimitFeature>(),
                    nameof(NicknameLimitFeature.AllowAnyChar),
                    out var entry))
                return;

            entry.SettingChanged += (_, __) =>
            {
                if (Engine.Enabled<NicknameLimitFeature>())
                    RefreshAllLobbyScenes("AllowAnyChar.Changed");
            };
            _allowAnyCharWired = true;
        }

        /// <summary>昵称输入框 = GetInputField(1)（原版 :502-505 的使用处）。</summary>
        public static TMP_InputField GetNicknameField(UI_LobbyScene scene)
        {
            try
            {
                return Traverse(scene)
                    .Method("GetInputField", new object[] { 1 })
                    .GetValue<TMP_InputField>();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>抬限前的原始上限（按 UI_LobbyScene 实例记录，OnDisabled 还原用）。</summary>
        private static readonly Dictionary<UI_LobbyScene, int> OriginalLimits =
            new Dictionary<UI_LobbyScene, int>();

        public static void LiftInputHardLimit(UI_LobbyScene scene, TMP_InputField field)
        {
            if (field == null)
                return;
            // 只抬不降：Prefab 原始小上限抬到 SoftCap；0 表示无限制（TMP 默认），保持不动；
            // 与 ChatLimit 同开时本框可能已被抬到更大值——保持更大值，避免两功能互相拉扯。
            if (field.characterLimit > 0 && field.characterLimit < SoftCap)
            {
                if (!OriginalLimits.ContainsKey(scene))
                    OriginalLimits[scene] = field.characterLimit;
                field.characterLimit = SoftCap;
                Log.Info<NicknameLimitFeature>($"昵称输入框 characterLimit → {SoftCap}");
            }
        }

        /// <summary>
        /// OnDisabled 还原：恢复抬限前的 characterLimit，并按原版权重规则
        /// （CJK×2 + 字母数字 &gt; 12 即超长，0.1.15b UI_LobbyScene.cs:1585）重算门禁与按钮着色，
        /// 不给热关闭后遗留"超长名可进房"的窗口。
        /// </summary>
        public static void RestoreAllLobbyScenes()
        {
            try
            {
                var scenes = Object.FindObjectsByType<UI_LobbyScene>(FindObjectsSortMode.None);
                foreach (var scene in scenes)
                {
                    var field = GetNicknameField(scene);
                    if (field != null && OriginalLimits.TryGetValue(scene, out int original))
                        field.characterLimit = original;

                    string name = field != null ? field.text : Managers.Player?.MyPlayerName;
                    bool ok = VanillaIsAcceptable(name, out string tip);
                    ApplyValidUi(scene, ok, tip ?? "");
                }
                OriginalLimits.Clear();
                Log.Info<NicknameLimitFeature>("已还原昵称输入框上限与原版权重门禁");
            }
            catch (global::System.Exception ex)
            {
                Log.Error<NicknameLimitFeature>("还原大厅失败: " + ex.Message);
            }
        }

        /// <summary>原版三档校验（0.1.15b UI_LobbyScene.cs:1559-1601），仅供 OnDisabled 还原使用。</summary>
        private static bool VanillaIsAcceptable(string name, out string failTip)
        {
            failTip = null;
            if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name))
                return false;   // 原版此时清空提示、只置灰按钮
            if (name.Any(c => Other.IsMatch(c.ToString())))
            {
                try { failTip = Managers.GetText("LobbyNicknameInvalidChar"); }
                catch { failTip = ""; }
                return false;
            }
            int wide = name.Count(c => Regex.IsMatch(
                c.ToString(), "[\\u3131-\\u318E\\uAC00-\\uD7A3\\u3040-\\u309F\\u30A0-\\u30FF\\u31F0-\\u31FF\\u4E00-\\u9FFF]"));
            int narrow = name.Count(c => Regex.IsMatch(c.ToString(), "[a-zA-Z0-9]"));
            if (wide * 2 + narrow > 12)
            {
                try { failTip = Managers.GetText("LobbyNicknameTooLong"); }
                catch { failTip = ""; }
                return false;
            }
            return true;
        }

        private static Traverse Traverse(UI_LobbyScene scene) => HarmonyLib.Traverse.Create(scene);

        /// <summary>写门禁字段并直接清/写提示，避免仍显示「昵称太长了」。</summary>
        public static void ApplyValidUi(UI_LobbyScene scene, bool valid, string tip)
        {
            var t = Traverse(scene);
            t.Field("_isNickNameValid").SetValue(valid);   // 0.1.15b UI_LobbyScene.cs:195

            try
            {
                object tipObj = t.Method("GetText", new object[] { TextErrorTip }).GetValue();
                if (tipObj is TMP_Text tipText)
                    tipText.text = tip ?? "";

                Color c = valid ? Color.white : new Color(0.25f, 0.25f, 0.25f, 1f);
                foreach (int id in ButtonTextIds)
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

        public static void RefreshScene(UI_LobbyScene scene, string reason)
        {
            if (scene == null)
                return;
            var field = GetNicknameField(scene);
            LiftInputHardLimit(scene, field);

            string name = field != null ? field.text : Managers.Player?.MyPlayerName;
            bool ok = IsNameAcceptable(name, out string tip);
            ApplyValidUi(scene, ok, tip ?? "");

            // 合法时同步 MyPlayerName（与原版写入时机一致，但仅在可接受时）
            if (ok && name != null)
                Managers.Player.MyPlayerName = name;

            Log.Info<NicknameLimitFeature>(
                $"[{reason}] ok={ok} len={name?.Length ?? 0} limit={field?.characterLimit ?? -1}");
        }

        public static void RefreshAllLobbyScenes(string reason)
        {
            try
            {
                var scenes = Object.FindObjectsByType<UI_LobbyScene>(FindObjectsSortMode.None);
                if (scenes == null || scenes.Length == 0)
                {
                    Log.Info<NicknameLimitFeature>($"[{reason}] 当前无 UI_LobbyScene");
                    return;
                }
                foreach (var scene in scenes)
                    RefreshScene(scene, reason);
            }
            catch (global::System.Exception ex)
            {
                Log.Error<NicknameLimitFeature>("扫描大厅失败: " + ex.Message);
            }
        }
    }
}
