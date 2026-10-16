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
    /// <summary>
    /// ② 「提交线索」二次确认栏。
    /// 提交动作走原版：确认后调用 UI_ClueSubItem.OnClickPropositionButton（私有，转储 :86488），判据、发包、置标记、关平板都由原版负责，不自己拼发包逻辑。
    /// 确认栏外观全部来自原版：底板借卡片自己的框 tablet_clue_popup_frame.sprite（不透明度 PanelAlpha 可配）；主体是一张原版线索卡片（Managers.UI.MakeSubItem&lt;UI_ClueSubItem> → SetInfo → InitPopupClue，照抄游戏用法，转储 :52838-52840），只作展示，关掉它自己的提交按钮；两个按钮克隆原版 PropositionButton（GetObject(3)），并显式设回正常态（clue_submit_normal.sprite + 白色，否则克隆会带上悬停时的 clue_submit_hover 米黄，转储 :86285-86293）。
    /// 尺寸跟随内容：先量卡片实际尺寸再算面板，过宽时等比缩放到平板宽 80% 内。挂在平板 transform 下，平板关闭时确认栏一并消失。
    /// </summary>
    internal static class TabletQolConfirm
    {
        private const string RootName = "DT_ClueSubmitConfirm";

        private const float Padding = 26f;
        private const float TitleH = 62f;
        private const float Gap = 16f;
        private const float MinPanelW = 420f;

        /// <summary>
        /// 为 true 时放行原方法（确认后重入用），避免递归弹框。
        /// </summary>
        public static bool Suppress;

        private static GameObject _root;

        public static void Hide()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
            _root = null;
        }

        public static void Show(UI_ClueSubItem card)
        {
            Hide();
            if (card == null)
                return;

            Transform parent = TabletRoot(card);
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
            UI_ClueSubItem embedded = TabletQolFeature.ConfirmShowCard
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
            // 面板 = **半透明平板蓝**，颜色取自游戏内平板线索卡片的实测色 #22303F（不是凭感觉调的）。
            // 也不借任何 sprite —— 借 clue_pin_bg 带红圈、借 tablet_clue_popup_frame 是纯白底，都翻过车。
            Color panelColor;
            if (!ColorUtility.TryParseHtmlString(TabletQolFeature.PanelColorHex, out panelColor))
                panelColor = new Color(0.133f, 0.188f, 0.247f, 1f);          // #22303F
            panelImg.color = new Color(panelColor.r, panelColor.g, panelColor.b,
                                       Mathf.Clamp01(TabletQolFeature.PanelAlpha));
            panelImg.raycastTarget = true;                // 面板吞点击：点面板不当作取消
            // 描边用游戏自己的灰 #9198A0（同样取自实测），平板那种细亮边
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
                () => ConfirmAndSubmit(card));
            MakeCloneButton(card, panelRt, "取消", new Vector2(btnW * 0.58f, Padding), new Vector2(btnW, btnH), Hide);
        }

        /// <summary>
        /// 确认栏挂到平板自己的 transform 下（平板一关就跟着消失）；取不到就退回卡片根节点。
        /// </summary>
        /// <param name="card">线索卡片。</param>
        /// <returns>确认栏应挂载的父节点；取不到时为卡片根节点。</returns>
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

        /// <summary>
        /// 把原版卡片嵌进来量尺寸（只作展示 ⇒ 关掉它自己的提交按钮）。
        /// </summary>
        /// <param name="source">原版线索卡片。</param>
        /// <param name="parent">嵌入的父节点。</param>
        /// <param name="maxW">最大宽度，单位像素。</param>
        /// <param name="size">卡片量得的尺寸，单位像素。</param>
        /// <returns>嵌入后的卡片展示实例。</returns>
        private static UI_ClueSubItem EmbedCard(UI_ClueSubItem source, RectTransform parent, float maxW, out Vector2 size)
        {
            size = Vector2.zero;
            try
            {
                UI_ClueSubItem embedded = Managers.UI.MakeSubItem<UI_ClueSubItem>(parent);
                var clue = source.Clue;                   // 类型是嵌套类型，用 var 免去命名
                embedded.SetInfo(clue);                   // SetInfo 内部已 Init + Refresh；0.1.18c 删除 InitPopupClue（悬停展开文本随 What 维度一并移除）

                GameObject submitGo = Traverse.Create(embedded).Method("GetObject", new object[] { 3 }).GetValue<GameObject>();
                if (submitGo != null)
                    submitGo.SetActive(false);            // 只作展示，别让它在确认栏里再发一次

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

        /// <summary>
        /// 平板那种细亮边：四条 2px 细边贴在面板四边（不用 sprite，颜色/粗细都确定）。
        /// </summary>
        /// <param name="parent">要加边框的面板。</param>
        /// <param name="color">边框颜色。</param>
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

        /// <summary>
        /// 克隆卡片上的原版按钮（`GetObject(3)`），换标签、设回正常态、绑自己的动作。
        /// </summary>
        private static void MakeCloneButton(UI_ClueSubItem source, Transform parent, string label,
            Vector2 pos, Vector2 size, Action onClick)
        {
            GameObject src = Traverse.Create(source).Method("GetObject", new object[] { 3 }).GetValue<GameObject>();
            GameObject clone = src != null ? UnityEngine.Object.Instantiate(src, parent)
                                           : new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image));

            RectTransform rt = (RectTransform)clone.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one;

            // 克隆会带上鼠标悬停瞬间的外观（clue_submit_hover = 米黄）⇒ 显式设回游戏的正常态；
            // 透明度用 ButtonAlpha —— 纯白实心在压暗的遮罩上太扎眼，半透明更像同一套 UI。
            Image img = clone.GetComponent<Image>();
            if (img != null)
            {
                Sprite normal = Managers.Resource.Load<Sprite>("clue_submit_normal.sprite");
                if (normal != null)
                    img.sprite = normal;
                // 按钮压成平板的蓝白调（#DCE6F5），别在深蓝面板上跳出一块纯白
                img.color = new Color(0.863f, 0.902f, 0.961f, Mathf.Clamp01(TabletQolFeature.ButtonAlpha));
            }

            TMP_Text[] texts = clone.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0)
                texts[0].text = label;

            Button btn = clone.GetComponent<Button>();
            if (btn != null)
                btn.onClick = new Button.ButtonClickedEvent();   // 清掉可能被序列化带过来的监听

            UI_Base.BindEvent(clone, _ => onClick());
        }

        /// <summary>
        /// 确认：关掉确认栏，然后调用**原版**提交函数。
        /// </summary>
        /// <remarks>
        /// 用 `AccessTools.Method` + `Invoke` 而不是 `Traverse.Method(name, args)`：后者按"参数个数 + 值"
        /// 挑重载，传 `null`（`PointerEventData` 用不到）时可能静默匹配不上。
        /// `Suppress` 期间放行 Prefix，避免"确认 → 又弹一次框"。
        /// </remarks>
        /// <param name="card">要提交线索的卡片。</param>
        private static void ConfirmAndSubmit(UI_ClueSubItem card)
        {
            Hide();
            if (card == null)
                return;

            try
            {
                MethodInfo target = AccessTools.Method(typeof(UI_ClueSubItem), "OnClickPropositionButton");
                if (target == null)
                {
                    Log.Error<TabletQolFeature>("找不到 UI_ClueSubItem.OnClickPropositionButton，无法提交");
                    return;
                }

                Suppress = true;
                target.Invoke(card, new object[] { null });   // 原方法体不使用该参数（转储 :86488-86500）
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
