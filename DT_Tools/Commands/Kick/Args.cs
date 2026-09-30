using DT_Tools.Game;

namespace DT_Tools.Commands.Kick
{
    /// <summary>/kick 参数：#数字 / 纯数字。</summary>
    internal static class KickArgs
    {
        public static bool TryParse(string raw, out int playerId, out string error)
        {
            if (TargetSpec.TryParseId(raw, out playerId))
            {
                error = null;
                return true;
            }
            error = $"无效的玩家 ID: {raw}（应为 #数字 或纯数字）";
            return false;
        }
    }
}
