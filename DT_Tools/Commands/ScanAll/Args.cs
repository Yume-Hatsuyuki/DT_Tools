using System.Globalization;

namespace DT_Tools.Commands.ScanAll
{
    /// <summary>/scan_all 参数：发包间隔（秒）。0=同帧一次发完，&gt;0=每包间隔。</summary>
    internal static class ScanAllArgs
    {
        /// <summary>间隔上限：超过只会让协程挂更久，无任何收益，拦下明显手滑的输入。</summary>
        private const float MaxInterval = 60f;

        public static bool TryParseInterval(string raw, out float interval, out string error)
        {
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out interval)
                || float.IsNaN(interval))
            {
                error = $"无效的间隔: {raw}（应为 0~{MaxInterval:0.#} 的秒数，0=同帧一次发完）";
                interval = 0f;
                return false;
            }
            if (interval < 0f || interval > MaxInterval)
            {
                error = $"间隔 {interval:0.##}s 超出范围（0~{MaxInterval:0.#} 秒，0=同帧一次发完）";
                interval = 0f;
                return false;
            }
            error = null;
            return true;
        }
    }
}
