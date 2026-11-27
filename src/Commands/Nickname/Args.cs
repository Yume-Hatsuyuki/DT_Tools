// 作用：解析 /Nickname 命令的参数；不负责命令执行与注册。
using DT_Tools.Game;

namespace DT_Tools.Commands.Nickname
{
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
