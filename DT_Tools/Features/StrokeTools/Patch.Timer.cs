using System;
using DT_Tools.Core;
using HarmonyLib;
using UnityEngine;

namespace DT_Tools.Features.StrokeTools
{
    [HarmonyPatch(typeof(DrawingManager), nameof(DrawingManager.EndLocalStroke))]
    internal static class StrokeTimerPatch
    {
        private const float FallbackRatio = 0.094f;

        private static readonly Func<UI_Base, int, GameObject> TabletGetObject =
            Reflect.Bind<Func<UI_Base, int, GameObject>>(typeof(UI_Base), "GetObject", new[] { typeof(int) });

        private static void Prefix(DrawingManager __instance)
        {
            if (!Engine.Enabled<StrokeToolsFeature>() || !StrokeToolsFeature.Timer)
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

            float scale = FallbackRatio;
            try
            {
                Data.MapData map = Managers.Data?.MapData;
                if (map != null && map.MinimapScale > 0f)
                    scale = map.MinimapScale;
            }
            catch (Exception)
            {
            }

            Vector2 prev = new Vector2(active.Points[0].x / scale, active.Points[0].y / scale);
            float length = 0f;
            for (int i = 1; i < active.Points.Count; i++)
            {
                Vector2 cur = new Vector2(active.Points[i].x / scale, active.Points[i].y / scale);
                length += Vector2.Distance(prev, cur);
                prev = cur;
            }

            float runSpeed = Mathf.Max(1f, StrokeToolsFeature.Speed);
            float walkSpeed = Mathf.Max(1f, StrokeToolsFeature.WalkSpeed);
            float seconds = EstimateWithStamina(length, runSpeed, walkSpeed);
            float walkOnlySeconds = length / walkSpeed;
            string message = $"画线测速（跑{(int)runSpeed}走{(int)walkSpeed}）：线长{length:F0}｜估约{seconds:F1}秒｜纯走{walkOnlySeconds:F1}秒";
            Managers.Voice?.SendChatMessage(message);
        }

        private static float EstimateWithStamina(float length, float runSpeed, float walkSpeed)
        {
            float avgSpeed = (runSpeed + walkSpeed) / 2f;
            return length / avgSpeed;
        }

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
