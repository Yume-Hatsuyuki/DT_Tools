// 作用：描述目标规格；不负责功能开关、配置与补丁挂载。
using System;

namespace DT_Tools.Game
{
    public static class TargetSpec
    {
        public static bool IsAll(string raw)
            => raw != null && raw.Equals("all", StringComparison.OrdinalIgnoreCase);

        public static bool TryParseId(string raw, out int id)
        {
            if (raw != null && raw.StartsWith("#") && int.TryParse(raw.Substring(1), out id))
                return true;
            return int.TryParse(raw, out id);
        }
    }
}
