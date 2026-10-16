using System;
using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.StrokeTools
{
    /// <summary>
    /// 路线解读：钩住 DrawingManager.EndLocalStroke（0.1.18c DrawingManager.cs:390，与画线测速同钩，各自 Prefix 互不干扰）。
    /// </summary>
    /// <remarks>
    /// Prefix 在 _active 置空（DrawingManager.cs:397）前读取笔画；该方法只由本地画布调用，远端笔画不经此处。
    /// 坐标换算同测速：Stroke.Points 为平板局部坐标，世界→平板比例 = MapData.MinimapScale（0.1.18c UI_TutorialTablet.cs:2146），兜底 0.094（0.1.18c Util.cs:887（待核对））；世界坐标 = 局部坐标 ÷ scale。
    /// 房间识别（与官方一致）：格子 = 世界坐标 ÷ 224 − MapData.MapOffset（0.1.16b Server.Game/AreaManager.cs:79（待核对） GetArea，ValidPosition :89 含边界）；格子值 = Managers.Data.MapArray[x][y]（DataManager.cs:28，byte 即 ERoomType，Protocol/ERoomType.cs），0 为 DefaultRoom。房间名 = Managers.GetText(枚举名)（0.1.18c UI_ChatDevicePopup.cs:475）。
    /// 沿线按 SampleStep 细分采样，连续同房间合并，走廊可按配置过滤。结果经聊天广播（Managers.Voice.SendChatMessage）全房可见。
    /// </remarks>
    [HarmonyPatch(typeof(DrawingManager), nameof(DrawingManager.EndLocalStroke))]
    internal static class StrokeRoutePatch
    {
        /// <summary>
        /// 官方世界→小地图比例兜底（0.1.16b Util.cs:887（待核对） / UI_GameTablet.cs:1189）。
        /// </summary>
        private const float FallbackRatio = 0.094f;

        private static void Prefix(DrawingManager __instance)
        {
            if (!Engine.Enabled<StrokeToolsFeature>() || !StrokeToolsFeature.Route)
                return;

            if (Managers.Player == null || Managers.Data?.MapData == null)
                return;

            DrawingManager.Stroke active = Traverse.Create(__instance).Field("_active").GetValue<DrawingManager.Stroke>();
            if (active == null || active.Points.Count < 2)
                return;
            if (active.PlayerId != Managers.Player.MyPlayerID)
                return;

            Data.MapData map = Managers.Data.MapData;
            if (map.MapOffset == null || Managers.Data.MapArray == null || Managers.Data.MapArray.Count == 0)
                return;

            // 世界→平板比例（同画线测速）：优先地图配置 MinimapScale，兜底 0.094。
            float scale = FallbackRatio;
            try
            {
                if (map.MinimapScale > 0f)
                    scale = map.MinimapScale;
            }
            catch (Exception)
            {
                // 维持兜底比例
            }

            float cell = Mathf.Max(1f, StrokeToolsFeature.CellSize);
            float step = Mathf.Max(1f, StrokeToolsFeature.SampleStep);
            Vector2 origin = StrokeRouteLogic.GetMapOrigin(map);

            // 沿线细分采样：相邻笔画点间按 step 步长补点，识别各自房间。
            // 世界坐标 = 局部 ÷ scale + MinimapOffset×224（官方 WorldToMap 的逆，见 Logic.GetMapOrigin）。
            List<string> route = new List<string>();
            Vector2 prev = new Vector2(active.Points[0].x / scale, active.Points[0].y / scale) + origin;
            StrokeRouteLogic.AppendRoomAt(route, prev, map, cell);
            for (int i = 1; i < active.Points.Count; i++)
            {
                Vector2 cur = new Vector2(active.Points[i].x / scale, active.Points[i].y / scale) + origin;
                float dist = Vector2.Distance(prev, cur);
                int samples = Mathf.Max(1, Mathf.CeilToInt(dist / step));
                for (int s = 1; s <= samples; s++)
                {
                    Vector2 p = Vector2.Lerp(prev, cur, s / (float)samples);
                    StrokeRouteLogic.AppendRoomAt(route, p, map, cell);
                }
                prev = cur;
            }

            if (route.Count < 2)
            {
                // 排障日志：房间数不足不广播，输出换算参数与起点坐标便于定位（正常画线很少触发）。
                Log.Info<StrokeToolsFeature>($"路线解读未广播：采样后房间数 {route.Count}，scale={scale:F4} origin=({origin.x:F0},{origin.y:F0})，offset=({map.MapOffset.X},{map.MapOffset.Y})，笔画点 {active.Points.Count} 个，起点局部=({active.Points[0].x:F1},{active.Points[0].y:F1})");
                return;
            }

            string path = string.Join(" - ", route);
            Log.Info<StrokeToolsFeature>($"路线解读广播：{path}");
            Managers.Voice?.SendChatMessage($"路线解读：{path}");
        }
    }
}
