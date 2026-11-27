// 作用：维护黑幕情报共享的持久状态；不负责补丁挂载与界面渲染。
using System.Collections.Generic;
using UnityEngine;

namespace DT_Tools.Features.DarkRadar
{
    internal static class DarkRadarState
    {
        public static readonly HashSet<Vector2> WeaponPins = new HashSet<Vector2>();
        public static readonly HashSet<Vector2> FuseboxPins = new HashSet<Vector2>();
        public static float NextCheck;

        public static void ResetClock() => NextCheck = 0f;

        public static void Reset()
        {
            ResetClock();
            WeaponPins.Clear();
            FuseboxPins.Clear();
        }
    }
}
