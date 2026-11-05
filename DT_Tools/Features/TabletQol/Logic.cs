using System;
using System.Reflection;
using DT_Tools.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DT_Tools.Features.TabletQol
{
    internal static class TabletQolConfirm
    {
        private const string RootName = "DT_ClueSubmitConfirm";

        private const float Padding = 26f;
        private const float TitleH = 62f;
        private const float Gap = 16f;
        private const float MinPanelW = 420f;

        public static bool Suppress;

        private static GameObject _root;
        private static System.Action _onConfirm;

        public static void Hide()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
            _root = null;
            _onConfirm = null;
        }

        public static void Show(UI_ClueSubItem card, Transform parentOverride, System.Action onConfirm)
        {
            Hide();
            Transform parent = parentOverride != null ? parentOverride : (card != null ? TabletRoot(card) : null);
            if (parent == null)
                return;

            _onConfirm = onConfirm;
            float tabletW = (parent as RectTransform)?.rect.width ?? 0f;
            if (tabletW <= 1f)
                tabletW = 900f;
            float maxPanelW = tabletW * 0.80f;

            _root = new GameObject(RootName, typeof(RectTransform));
            RectTransform rootRt = (RectTransform)_root.transform;
            rootRt.SetParent(parent, false);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootRt.localScale = Vector3.one;
            rootRt.SetAsLastSibling();

            GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            RectTransform dimRt = (RectTransform)dim.transform;
            dimRt.SetParent(rootRt, false);
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            dimRt.localScale = Vector3.one;
            Image dimImg = dim.GetComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, Mathf.Clamp01(TabletQolFeature.DimAlpha));
            dimImg.raycastTarget = true;
            UI_Base.BindEvent(dim, _ => Hide());

            Vector2 cardSize = Vector2.zero;
            UI_ClueSubItem embedded = (card != null && TabletQolFeature.ConfirmShowCard)
                ? EmbedCard(card, rootRt, maxPanelW, out cardSize)
                : null;

            float btnH = Mathf.Clamp(cardSize.y > 1f ? cardSize.y * 0.9f : 60f, 48f, 96f);
            float contentW = Mathf.Max(cardSize.x, MinPanelW * 0.62f);
            float panelW = Mathf.Min(contentW + Padding * 2f, maxPanelW);
            float panelH = Padding + TitleH + Gap + cardSize.y + Gap + btnH + Padding;

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            RectTransform panelRt = (RectTransform)panel.transform;
            panelRt.SetParent(rootRt, false);
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(panelW, panelH);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.localScale = Vector3.one;

            Image panelImg = panel.GetComponent<Image>();
            Color panelColor;
            if (!ColorUtility.TryParseHtmlString(TabletQolFeature.PanelColorHex, out panelColor))
                panelColor = new Color(0.133f, 0.188f, 0.247f, 1f);
            panelImg.color = new Color(panelColor.r, panelColor.g, panelColor.b,
                                       Mathf.Clamp01(TabletQolFeature.PanelAlpha));
            panelImg.raycastTarget = true;
            AddBorder(panelRt, new Color(0.569f, 0.596f, 0.627f, 0.60f));

            MakeLabel(panelRt, "确认提交该线索？", 28f, Color.white, Padding + 2f);
            MakeLabel(panelRt, "提交后不可撤回", 20f, new Color(1f, 0.78f, 0.35f, 1f), Padding + 36f);

            if (embedded != null)
            {
                RectTransform crt = (RectTransform)embedded.transform;
                crt.SetParent(panelRt, false);
                crt.anchorMin = new Vector2(0.5f, 1f);
                crt.anchorMax = new Vector2(0.5f, 1f);
                crt.pivot = new Vector2(0.5f, 1f);
                crt.anchoredPosition = new Vector2(0f, -(Padding + TitleH + Gap));
            }

            float btnW = Mathf.Min(panelW * 0.32f, 260f);
            MakeCloneButton(card, panelRt, "确认提交", new Vector2(-btnW * 0.58f, Padding), new Vector2(btnW, btnH),
                ConfirmAndSubmit);
            MakeCloneButton(card, panelRt, "取消", new Vector2(btnW * 0.58f, Padding), new Vector2(btnW, btnH), Hide);
        }

        private static Transform TabletRoot(UI_ClueSubItem card)
        {
            try
            {
                if (Managers.Tablet != null && Managers.Tablet.Tablet != null)
                    return Managers.Tablet.Tablet.transform;
            }
            catch (Exception ex)
            {
                Log.Warn<TabletQolFeature>(ex, "取平板根失败，退回卡片根节点");
            }
            return card.transform.root;
        }

        private static UI_ClueSubItem EmbedCard(UI_ClueSubItem source, RectTransform parent, float maxW, out Vector2 size)
        {
            size = Vector2.zero;
            try
            {
                UI_ClueSubItem embedded = Managers.UI.MakeSubItem<UI_ClueSubItem>(parent);
                var clue = source.Clue;
                embedded.SetInfo(clue);

                RectTransform rt = (RectTransform)embedded.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;

                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                float w = rt.rect.width;
                float h = rt.rect.height;
                float scale = (w > maxW && maxW > 1f) ? (maxW / w) : 1f;
                if (scale < 1f)
                    rt.localScale = new Vector3(scale, scale, 1f);
                size = new Vector2(w * scale, h * scale);
                return embedded;
            }
            catch (Exception ex)
            {
                Log.Warn<TabletQolFeature>(ex, "内嵌原版线索卡片失败（确认栏仍可用）");
                return null;
            }
        }

        private static void AddBorder(RectTransform parent, Color color)
        {
            const float t = 2f;
            AddEdge(parent, "BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, t), new Vector2(0f, -t * 0.5f), color);
            AddEdge(parent, "BorderBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, t), new Vector2(0f, t * 0.5f), color);
            AddEdge(parent, "BorderLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(t, 0f), new Vector2(-t * 0.5f, 0f), color);
            AddEdge(parent, "BorderRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(t, 0f), new Vector2(t * 0.5f, 0f), color);
        }

        private static void AddEdge(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 size, Vector2 pos, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one;
            Image img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private static void MakeLabel(Transform parent, string text, float size, Color color, float topOffset)        {
            GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-Padding * 2f, size + 10f);
            rt.anchoredPosition = new Vector2(0f, -topOffset);
            rt.localScale = Vector3.one;

            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = color;
            t.raycastTarget = false;
        }

        private static void MakeCloneButton(UI_ClueSubItem source, Transform parent, string label,
            Vector2 pos, Vector2 size, Action onClick)
        {
            // 0.1.21h 的线索卡片不再自带提交按钮，这里直接生成带文字的按钮。
            GameObject button = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image));

            RectTransform rt = (RectTransform)button.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one;

            Image img = button.GetComponent<Image>();
            Sprite normal = Managers.Resource.Load<Sprite>("clue_submit_normal.sprite");
            if (normal != null)
                img.sprite = normal;
            img.color = new Color(0.863f, 0.902f, 0.961f, Mathf.Clamp01(TabletQolFeature.ButtonAlpha));

            GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform textRt = (RectTransform)textGo.transform;
            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            textRt.localScale = Vector3.one;

            TextMeshProUGUI text = textGo.GetComponent<TextMeshProUGUI>();
            TMP_Text sample = source != null ? source.GetComponentInChildren<TMP_Text>(true) : null;
            if (sample != null && sample.font != null)
                text.font = sample.font;
            text.text = label;
            text.fontSize = 22f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            UI_Base.BindEvent(button, _ => onClick());
        }

        private static void ConfirmAndSubmit()
        {
            Action onConfirm = _onConfirm;
            Hide();
            try
            {
                onConfirm?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error<TabletQolFeature>(ex, "确认后执行原版提交失败");
            }
        }

        public static void Submit(UI_TrialScreenPopup popup)
        {
            if (popup == null)
                return;

            try
            {
                Suppress = true;
                Traverse.Create(popup).Method("SubmitFocusedClue").GetValue();
                Log.Info<TabletQolFeature>(
                    $"确认提交：已调用原版提交函数（IsSubmitProposition={Managers.Game.IsSubmitProposition}）");
            }
            catch (Exception ex)
            {
                Log.Error<TabletQolFeature>(ex, "确认后执行原版提交失败");
            }
            finally
            {
                Suppress = false;
            }
        }
    }
}
