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
