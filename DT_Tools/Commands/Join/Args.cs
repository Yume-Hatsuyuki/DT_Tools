using DT_Tools.Game;

namespace DT_Tools.Commands.Join
{
    /// <summary>/join 参数：房间码（清洗规则与 CustomRoomCode 写入侧一致：大写 A–Z / 0–9，≤7 位）。</summary>
    internal static class JoinArgs
    {
        public static bool TryParseCode(string raw, out string code, out string error)
        {
            code = null;
            error = null;
            string trimmed = raw?.Trim().ToUpperInvariant() ?? "";
            var sb = new System.Text.StringBuilder(trimmed.Length);
            foreach (char c in trimmed)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    sb.Append(c);
                if (sb.Length == 7)
                    break;
            }
            if (sb.Length == 0)
            {
                error = $"无效的房间码: {raw}（应为字母数字组合）。";
                return false;
            }
            code = sb.ToString();
            return true;
        }
    }
}
