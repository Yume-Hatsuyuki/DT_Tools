using System;

namespace DT_Tools.Game
{
    /// <summary>命令目标解析：all / (#数字 或 纯数字)。全项目唯一实现。</summary>
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
