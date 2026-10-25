using System;
using System.Collections.Generic;
using DT_Tools.Core;
using HarmonyLib;
using Protocol;
using UnityEngine;

namespace DT_Tools.Features.StrokeTools
{
    [HarmonyPatch(typeof(DrawingManager), nameof(DrawingManager.EndLocalStroke))]
    internal static class StrokeRoutePatch
    {
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

            float scale = FallbackRatio;
            try
            {
                if (map.MinimapScale > 0f)
                    scale = map.MinimapScale;
            }
            catch (Exception)
            {
            }

            float cell = Mathf.Max(1f, StrokeToolsFeature.CellSize);
            float step = Mathf.Max(1f, StrokeToolsFeature.SampleStep);
            Vector2 origin = StrokeRouteLogic.GetMapOrigin(map);

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
                Log.Info<StrokeToolsFeature>($"路线解读未广播：采样后房间数 {route.Count}，scale={scale:F4} origin=({origin.x:F0},{origin.y:F0})，offset=({map.MapOffset.X},{map.MapOffset.Y})，笔画点 {active.Points.Count} 个，起点局部=({active.Points[0].x:F1},{active.Points[0].y:F1})");
                return;
            }

            string path = string.Join(" - ", route);
            Log.Info<StrokeToolsFeature>($"路线解读广播：{path}");
            Managers.Voice?.SendChatMessage($"路线解读：{path}");
        }
    }
}
