using DT_Tools.Game;

namespace DT_Tools.Commands.Nickname
{
    /// <summary>/nick 参数：目标 #数字 / 纯数字；新昵称取剩余参数以空格拼接。</summary>
    internal static class NicknameArgs
    {
        public static bool TryParseTarget(string raw, out int playerId, out string error)
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
