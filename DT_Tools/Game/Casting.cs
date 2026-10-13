namespace DT_Tools.Game
{
    /// <summary>
    /// 客户端读条时长参数收口：DestroyEvidence / RepairFuse / ScanTime 三处读条类补丁
    /// 相同的钳制逻辑上浮于此（AGENTS.md §4：≥2 处才上浮）。
    /// </summary>
    /// <remarks>
    /// 手改 .cfg 可能写出负数 / NaN / Infinity，
    /// 统一钳到合法下限，非法值回退 fallback（各补丁语义不变）。
    /// </remarks>
    public static class Casting
    {
        /// <summary>
        /// 非法（&lt;min / NaN / Infinity，注意 NaN 与任何比较均为 false 故需显式判）时返回 fallback。
        /// </summary>
        /// <param name="seconds">要夹紧的秒数。</param>
        /// <param name="min">允许的最小秒数。</param>
        /// <param name="fallback">非法输入时使用的秒数。</param>
        /// <returns>夹紧后的秒数；非法输入时为 fallback。</returns>
        public static float ClampSeconds(float seconds, float min, float fallback)
            => (seconds < min || float.IsNaN(seconds) || float.IsInfinity(seconds)) ? fallback : seconds;
    }
}
