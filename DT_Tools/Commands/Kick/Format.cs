using Server.Game;

namespace DT_Tools.Commands.Kick
{
    /// /kick 输出格式化：人类回复文本 + JSON 结果 DTO。
    internal static class KickFormat
    {
        /// 目标显示名（退出/未命名时回退 #id）。
        public static string DisplayName(Server.Game.Player target, int fallbackId)
            => target?.Name ?? ($"#{fallbackId}");

        public static object Result(Server.Game.Player target, int targetId)
            => new { pid = targetId, name = target?.Name ?? "" };
    }
}
