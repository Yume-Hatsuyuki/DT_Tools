using System;
using UnityEngine;

namespace DT_Tools.Patches.Experience.TypewriterFloat
{
    /// <summary>
    /// 打字机浮文编排：自己回声识别（文本+3 秒窗口）、新鲜度/喂料面板过滤、
    /// 复用 UI_SecretChatBubble 匿名浮文（随机位置倾斜逐字上屏后淡出）。
    /// </summary>
    internal static class TypewriterFloatLogic
    {
        private static Transform _fallback;

        private static string _lastSelfText = "";
        private static float _lastSelfRealtime = float.NegativeInfinity;

        /// <summary>本机发送对讲机消息时记录（识别服务器回显的自己的消息）。</summary>
        public static void MarkSelfSend(string message)
        {
            _lastSelfText = message ?? "";
            _lastSelfRealtime = Time.unscaledTime;
        }

        /// <summary>
        /// 公开频道消息浮文化：过滤自己的回声与过期积压、喂料面板打开时不重复显示，
        /// 其余复用秘密通话浮文组件（Spawn 路径与 UI_SecretChatOverlay.cs:80 一致）。
        /// </summary>
        public static void OnDeviceChat(int time, string message)
        {
            if (Time.unscaledTime - _lastSelfRealtime <= 3f && message == _lastSelfText)
                return;    // 自己的消息回显：不浮文（与秘密通话无自身回声的行为对齐）
            if (Managers.Game != null && time < Managers.Game.SurvivalTime - 3)
                return;    // 过期积压（与原版覆盖层的 3 秒新鲜度过滤一致）
            if (Managers.UI != null && Managers.UI.FindOpenInteractUI<UI_ChatDevicePopup>() != null)
                return;    // 喂料面板已打开：列表里看得到，不重复浮文

            Transform parent = GetOverlayTransform();
            if (parent == null)
                return;
            var bubble = Managers.UI.MakeSubItem<UI_SecretChatBubble>(parent);
            if (bubble == null)
                return;    // 资源缺失：静默放弃
            bubble.transform.SetAsLastSibling();
            bubble.Show(message, delegate { });
        }

        /// <summary>
        /// 浮文父容器：优先复用场景内现成的 UI_SecretChatOverlay（sortingOrder=1999，
        /// UI_GameScene object30）；无覆盖层时按其 Init 装配方式惰性自建（随场景销毁）。
        /// </summary>
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
