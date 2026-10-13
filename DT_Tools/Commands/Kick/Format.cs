using Server.Game;

namespace DT_Tools.Commands.Kick
{
    /// <summary>
    /// /kick 输出格式化：人类回复文本 + JSON 结果 DTO。
    /// </summary>
    internal static class KickFormat
    {
        /// <summary>
        /// 目标显示名（退出/未命名时回退 #id）。
        /// </summary>
        /// <param name="target">被踢的玩家。</param>
        /// <param name="fallbackId">取不到名字时回退显示的 ID。</param>
        /// <returns>目标显示名；退出或未命名时为 #fallbackId。</returns>
        public static string DisplayName(Server.Game.Player target, int fallbackId)
            => target?.Name ?? ($"#{fallbackId}");

        public static object Result(Server.Game.Player target, int targetId)
            => new { pid = targetId, name = target?.Name ?? "" };
    }
}
