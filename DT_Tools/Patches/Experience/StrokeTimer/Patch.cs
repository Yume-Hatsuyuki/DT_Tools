using System;
using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Patches.Experience.StrokeTimer
{
    /// <summary>
    /// 钩住 DrawingManager.EndLocalStroke（本地玩家松笔画完时调用，0.1.16b DrawingManager.cs:390）。
    /// 用 Prefix 在 _active 置空（DrawingManager.cs:397）前读取当前笔画；EndLocalStroke 只由本地画布
    /// （UI_DrawSurface）调用，远端笔画走网络通道不经此方法，天然只测自己画的线。
    ///
    /// 坐标系换算：Stroke.Points 是平板地图画布（对象88 MapBoundsRect）的局部坐标
    /// （0.1.16b UI_DrawSurface.cs:43 ScreenPointToLocalPointInRectangle），画布与地图显示区域（对象10）重叠，
    /// 官方世界→平板比例 = MapData.MinimapScale（每图配置，0.1.16b UI_TutorialTablet.cs:2157 _mapScale），
    /// 兜底 0.094（0.1.16b Util.cs:887 GetMinimapPosition / UI_GameTablet.cs:1189 InitConstMap 世界×0.094）。
    /// 因此世界线长 = Σ|相邻点局部差| ÷ scale，平移（pivot/原点）不影响长度。
    /// 速度基准：活人走路 560、奔跑 560×1.3=728（0.1.16b BuffComponent.cs:171-180），840 是幽灵速度。
    /// 结果走聊天广播（Managers.Voice.SendChatMessage），全房可见。
    /// </summary>
    [HarmonyPatch(typeof(DrawingManager), nameof(DrawingManager.EndLocalStroke))]
    internal static class StrokeTimerPatch
    {
        /// <summary>官方世界→小地图比例兜底（0.1.16b Util.cs:887 / UI_GameTablet.cs:1189）。</summary>
        private const float FallbackRatio = 0.094f;

        /// <summary>UI_Base.GetObject(int) protected：0.1.16b UI_Base.cs:93（平板画布 GetObject(88) 取 RectTransform）。</summary>
        private static readonly Func<UI_Base, int, GameObject> TabletGetObject =
            Reflect.Bind<Func<UI_Base, int, GameObject>>(typeof(UI_Base), "GetObject", new[] { typeof(int) });

        private static void Prefix(DrawingManager __instance)
        {
            if (!Engine.Enabled<StrokeTimerFeature>())
                return;

            if (Managers.Player == null)
                return;

            DrawingManager.Stroke active = Traverse.Create(__instance).Field("_active").GetValue<DrawingManager.Stroke>();
            if (active == null || active.Points.Count < 2)
                return;
            if (active.PlayerId != Managers.Player.MyPlayerID)
                return;

            RectTransform mapBounds = GetMapBoundsRect();
            if (mapBounds == null)
                return;

            // 世界→平板比例：优先地图配置 MinimapScale（每图不同，0.1.16b UI_TutorialTablet.cs:2157），
            // MapData 未加载/未配置时兜底官方 0.094（Util.cs:887）。世界线长 = Σ|局部差| ÷ scale。
            float scale = FallbackRatio;
            try
            {
                Data.MapData map = Managers.Data?.MapData;
                if (map != null && map.MinimapScale > 0f)
                    scale = map.MinimapScale;
            }
            catch (Exception)
            {
                // 地图数据不可用时维持兜底比例
            }

            Vector2 prev = new Vector2(active.Points[0].x / scale, active.Points[0].y / scale);
            float length = 0f;
            for (int i = 1; i < active.Points.Count; i++)
            {
                Vector2 cur = new Vector2(active.Points[i].x / scale, active.Points[i].y / scale);
                length += Vector2.Distance(prev, cur);
                prev = cur;
            }

            float runSpeed = Mathf.Max(1f, StrokeTimerFeature.Speed);
            float walkSpeed = Mathf.Max(1f, StrokeTimerFeature.WalkSpeed);
            float seconds = EstimateWithStamina(length, runSpeed, walkSpeed);
            float walkOnlySeconds = length / walkSpeed;
            string message = $"画线测速（跑{(int)runSpeed}走{(int)walkSpeed}）：线长{length:F0}｜估约{seconds:F1}秒｜纯走{walkOnlySeconds:F1}秒";
            Managers.Voice?.SendChatMessage(message);
        }

        /// <summary>
        /// 移动时间估算（按实测校准的"跑走各半"节奏）：
        /// 实测反馈线长 3843 单位跑约 6 秒 → 实际平均速度 ≈ 640 ≈ (奔跑 728 + 走路 560) / 2 = 644。
        /// 玩家日常不会全程紧贴耐力极限（不会真的"满耐纯跑 5 秒"再卡恢复），实际节奏接近跑走各半，
        /// 故全程按均速估算；短距离冲刺会略偏保守，整体贴合日常实测。
        /// 耐力参数佐证：奔跑消耗 0.2/秒（0.1.16b UI_Stamina.cs:122）、未耗尽走路恢复 1/7（UI_Stamina.cs:98），
        /// 理论卡恢复极限均速 630 也贴近实测，均速模型在此区间内。
        /// </summary>
        private static float EstimateWithStamina(float length, float runSpeed, float walkSpeed)
        {
            float avgSpeed = (runSpeed + walkSpeed) / 2f;
            return length / avgSpeed;
        }

        /// <summary>
        /// 取平板地图画布 RectTransform（0.1.16b UI_GameTablet.cs:442-451 MapBoundsRect → GetObject(88)）。
        /// 平板未打开时 Managers.Tablet.Tablet 为空，跳过本次测速。
        /// </summary>
        private static RectTransform GetMapBoundsRect()
        {
            UI_GameTablet tablet = Managers.Tablet?.Tablet;
            if (tablet == null)
                return null;
            GameObject go = TabletGetObject?.Invoke(tablet, 88);
            return go != null ? go.GetComponent<RectTransform>() : null;
        }
    }
}
