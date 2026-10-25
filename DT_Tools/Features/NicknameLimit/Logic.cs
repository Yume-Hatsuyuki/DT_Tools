using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DT_Tools.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace DT_Tools.Features.NicknameLimit
{
    internal static class NicknameLimitLogic
    {
        public const int SoftCap = 64;

        private const int TextErrorTip = 15;
        private const int TextCreateGame = 16;
        private const int TextFindGame = 17;
        private const int TextEnterGame = 18;

        private static readonly int[] ButtonTextIds = { TextCreateGame, TextFindGame, TextEnterGame };

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
            if (!NicknameLimitFeature.AllowAnyChar && name.Any(c => Other.IsMatch(c.ToString())))
            {
                try { failTip = Managers.GetText("LobbyNicknameInvalidChar"); }
                catch { failTip = ""; }
                return false;
            }
            return true;
        }

        private static bool _allowAnyCharWired;

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

        private static readonly Dictionary<UI_LobbyScene, int> OriginalLimits =
            new Dictionary<UI_LobbyScene, int>();

        public static void LiftInputHardLimit(UI_LobbyScene scene, TMP_InputField field)
        {
            if (field == null)
                return;
            if (field.characterLimit > 0 && field.characterLimit < SoftCap)
            {
                if (!OriginalLimits.ContainsKey(scene))
                    OriginalLimits[scene] = field.characterLimit;
                field.characterLimit = SoftCap;
                Log.Info<NicknameLimitFeature>($"昵称输入框 characterLimit → {SoftCap}");
            }
        }

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
                Log.Error<NicknameLimitFeature>(ex, "还原大厅失败");
            }
        }

        private static bool VanillaIsAcceptable(string name, out string failTip)
        {
            failTip = null;
            if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name))
                return false;
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

        public static void ApplyValidUi(UI_LobbyScene scene, bool valid, string tip)
        {
            var t = Traverse(scene);
            t.Field("_isNickNameValid").SetValue(valid);

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

            if (ok && name != null)
                Managers.Player.MyPlayerName = name;

            Log.Info<NicknameLimitFeature>(
                $"{reason}：ok={ok} len={name?.Length ?? 0} limit={field?.characterLimit ?? -1}");
        }

        public static void RefreshAllLobbyScenes(string reason)
        {
            try
            {
                var scenes = Object.FindObjectsByType<UI_LobbyScene>(FindObjectsSortMode.None);
                if (scenes == null || scenes.Length == 0)
                {
                    Log.Info<NicknameLimitFeature>($"{reason}：当前无 UI_LobbyScene");
                    return;
                }
                foreach (var scene in scenes)
                    RefreshScene(scene, reason);
            }
            catch (global::System.Exception ex)
            {
                Log.Error<NicknameLimitFeature>(ex, "扫描大厅失败");
            }
        }
    }
}
