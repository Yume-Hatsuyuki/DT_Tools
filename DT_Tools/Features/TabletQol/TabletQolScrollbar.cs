using DT_Tools.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DT_Tools.Features.TabletQol
{
    internal static class TabletQolScrollbar
    {
        private const string BarName = "DT_TabletScrollbar";

        private const float TrackAlpha = 0.10f;
        private const float HandleAlpha = 0.45f;

        public static bool Attach(ScrollRect scroll, string label, string reason)
        {
            if (scroll == null)
                return false;
            if (scroll.verticalScrollbar != null || scroll.horizontalScrollbar != null)
                return false;

            bool axisForced = !scroll.vertical && !scroll.horizontal;
            bool vertical = scroll.vertical || axisForced;

            int width = Mathf.Clamp(TabletQolFeature.BarWidth, 4, 40);
            int inset = Mathf.Clamp(TabletQolFeature.BarOffset, 0, 60);
            Transform parent = scroll.viewport != null ? scroll.viewport : scroll.transform;

            GameObject go = new GameObject(BarName, typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            if (vertical)
            {
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(width, 0f);
                rt.anchoredPosition = new Vector2(-inset, 0f);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(0f, width);
                rt.anchoredPosition = new Vector2(0f, inset);
            }

            Image track = go.GetComponent<Image>();
            track.color = new Color(1f, 1f, 1f, TrackAlpha);
            track.raycastTarget = TabletQolFeature.BarTrackClickPages;

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            RectTransform hrt = (RectTransform)handle.transform;
            hrt.SetParent(rt, false);
            hrt.localScale = Vector3.one;
            hrt.anchorMin = Vector2.zero;
            hrt.anchorMax = Vector2.one;
            hrt.offsetMin = new Vector2(1f, 1f);
            hrt.offsetMax = new Vector2(-1f, -1f);
            Image hi = handle.GetComponent<Image>();
            hi.color = new Color(1f, 1f, 1f, HandleAlpha);
            hi.raycastTarget = true;

            Scrollbar bar = go.GetComponent<Scrollbar>();
            bar.handleRect = hrt;
            bar.targetGraphic = hi;
            bar.direction = vertical ? Scrollbar.Direction.BottomToTop : Scrollbar.Direction.LeftToRight;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };

            ScrollRect.ScrollbarVisibility visibility = TabletQolFeature.BarAlwaysVisible
                ? ScrollRect.ScrollbarVisibility.Permanent
                : ScrollRect.ScrollbarVisibility.AutoHide;
            if (vertical)
            {
                scroll.verticalScrollbar = bar;
                scroll.verticalScrollbarVisibility = visibility;
                scroll.verticalScrollbarSpacing = 2f;
            }
            else
            {
                scroll.horizontalScrollbar = bar;
                scroll.horizontalScrollbarVisibility = visibility;
                scroll.horizontalScrollbarSpacing = 2f;
            }

            Log.Info<TabletQolFeature>(
                $"平板列表已挂滚动条：{label}（{reason}；{(vertical ? "右侧竖条" : "底部横条")}，宽 {width}px，内缩 {inset}px，" +
                $"{(TabletQolFeature.BarAlwaysVisible ? "常驻" : "内容超出时才出现")}，" +
                $"{(TabletQolFeature.BarTrackClickPages ? "轨道可点翻页" : "轨道不吃点击")}；" +
                $"当时轴=竖{scroll.vertical}/横{scroll.horizontal}{(axisForced ? "（都没开，按竖排处理）" : "")}）");
            return true;
        }
    }
}
