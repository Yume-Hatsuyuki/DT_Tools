// 作用：格式化 /Kick 命令的回复文本；不负责命令执行与注册。
using Server.Game;

namespace DT_Tools.Commands.Kick
{
    internal static class KickFormat
    {
        public static string DisplayName(Server.Game.Player target, int fallbackId)
            => target?.Name ?? ($"#{fallbackId}");

        public static object Result(Server.Game.Player target, int targetId)
            => new { pid = targetId, name = target?.Name ?? "" };
    }
}
