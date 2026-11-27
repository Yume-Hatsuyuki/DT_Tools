// 作用：实现打字机消息浮文化的运行时逻辑；不负责补丁挂载与配置声明。
using System.Collections;
using System.Collections.Generic;
using DT_Tools.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DT_Tools.Features.TypewriterFloat
{
    internal static class TypewriterFloatLogic
    {
        private const float BubbleSeconds = 4f;
        private const float FadeSeconds = 0.4f;
        private const float RiseSpeed = 14f;
        private const float BubbleWidth = 760f;
        private const float BubbleHeight = 120f;
        private const float BubbleGap = 96f;

        private static Transform _layer;
        private static readonly List<RectTransform> Active = new List<RectTransform>();

        private static string _lastSelfText = "";
        private static float _lastSelfRealtime = float.NegativeInfinity;

        public static void MarkSelfSend(string message)
        {
            _lastSelfText = message ?? "";
            _lastSelfRealtime = Time.unscaledTime;
        }

        public static void OnDeviceChat(int time, string message)
        {
            if (Time.unscaledTime - _lastSelfRealtime <= 3f && message == _lastSelfText)
                return;
            if (Managers.Game != null && time < Managers.Game.SurvivalTime - 3)
                return;
            if (Managers.UI != null && Managers.UI.FindOpenInteractUI<UI_ChatDevicePopup>() != null)
                return;
            Show(message);
        }

        private static void Show(string message)
        {
            Transform layer = GetLayer();
            if (layer == null || string.IsNullOrEmpty(message))
                return;

            var go = new GameObject("DT_TypewriterFloatBubble", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(layer, false);
            rect.sizeDelta = new Vector2(BubbleWidth, BubbleHeight);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 220f + Active.Count * BubbleGap);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 30f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.text = message;

            Active.Add(rect);
            CoroutineHost.Start(Fade(rect));
        }

        private static IEnumerator Fade(RectTransform rect)
        {
            var group = rect.GetComponent<CanvasGroup>();
            float elapsed = 0f;
            while (elapsed < BubbleSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                if (rect == null)
                    yield break;
                rect.anchoredPosition += new Vector2(0f, RiseSpeed * Time.unscaledDeltaTime);
                if (group != null && elapsed > BubbleSeconds - FadeSeconds)
                    group.alpha = Mathf.Clamp01((BubbleSeconds - elapsed) / FadeSeconds);
                yield return null;
            }
            Active.Remove(rect);
            if (rect != null)
                Object.Destroy(rect.gameObject);
        }

        private static Transform GetLayer()
        {
            if (_layer != null)
                return _layer;

            var go = new GameObject("DT_TypewriterFloatOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1999;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var group = go.GetComponent<CanvasGroup>();
            group.ignoreParentGroups = true;
            group.blocksRaycasts = false;
            group.interactable = false;
            Object.DontDestroyOnLoad(go);
            _layer = go.transform;
            return _layer;
        }
    }
}
