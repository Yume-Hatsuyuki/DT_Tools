using System.Collections.Generic;
using Protocol;

namespace DT_Tools.Patches.Experience.CanShowLie
{
    /// <summary>运行期状态：LoadAllArea 时缓存的初始区域数据，供非黑幕补全伪证可选物证。</summary>
    internal static class CanShowLieState
    {
        public static List<AreaInitInfo> AreaCache;
    }
}
