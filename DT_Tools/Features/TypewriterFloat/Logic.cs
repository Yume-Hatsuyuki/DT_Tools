using System;
using UnityEngine;

namespace DT_Tools.Features.TypewriterFloat
{
    internal static class TypewriterFloatLogic
    {
        private static Transform _fallback;

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

            Transform parent = GetOverlayTransform();
            if (parent == null)
                return;
            var bubble = Managers.UI.MakeSubItem<UI_SecretChatBubble>(parent);
            if (bubble == null)
                return;
            bubble.transform.SetAsLastSibling();
            bubble.Show(message, delegate { });
        }

        private static Transform GetOverlayTransform()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<UI_SecretChatOverlay>();
            if (existing != null)
                return existing.transform;

            if (_fallback == null)
            {
                var go = new GameObject("DT_TypewriterFloatOverlay");
                var canvas = go.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 1999;
                var group = go.AddComponent<CanvasGroup>();
                group.ignoreParentGroups = true;
                group.blocksRaycasts = false;
                group.interactable = false;
                _fallback = go.transform;
            }
            return _fallback;
        }
    }
}
