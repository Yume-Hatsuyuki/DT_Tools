using System.Collections.Generic;
using UnityEngine;

namespace DT_Tools.Patches.Experience.DarkRadar
{
    /// <summary>运行期状态：本功能添加的 pin 集合（避免误删原版 Hope 等其他 common pin）与巡检时钟。</summary>
    internal static class DarkRadarState
    {
        public static readonly HashSet<Vector2> WeaponPins = new HashSet<Vector2>();
        public static readonly HashSet<Vector2> FuseboxPins = new HashSet<Vector2>();
        public static float NextCheck;

        public static void ResetClock() => NextCheck = 0f;

        /// <summary>清空全部跟踪（pin 的移除由 Logic 负责，涉及游戏调用）。</summary>
        public static void Reset()
        {
            ResetClock();
            WeaponPins.Clear();
            FuseboxPins.Clear();
        }
    }
}
