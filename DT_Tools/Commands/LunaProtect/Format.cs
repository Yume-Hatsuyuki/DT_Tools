using Server.Game;

namespace DT_Tools.Commands.LunaProtect
{
    /// <summary>/luna_protect 输出格式化：人类回复文本 + JSON 结果 DTO。</summary>
    internal static class LunaProtectFormat
    {
        private const string CaveatAll =
            "注意：与原版 Luna 完全同机制——停电期间护盾无效；不防致命诡计（Deadly Trick）；" +
            "效果持续到本局结束或玩家重连，期间后加入的玩家需重新施加。";

        private const string CaveatSingle =
            "注意：与原版 Luna 完全同机制——停电期间护盾无效；不防致命诡计（Deadly Trick）；" +
            "效果持续到本局结束或该玩家重连，期间后加入的玩家需重新施加。";

        public static string ReplyAll(int count)
            => $"已对全体 {count} 名存活玩家套用 Luna 护盾：亮灯期间 Black 无法直接刀杀他们。\n{CaveatAll}";

        public static string ReplySingle(Server.Game.Player target, int targetId)
            => $"已对 {target.Name}（#{targetId}）套用 Luna 护盾：亮灯期间 Black 无法直接刀杀该玩家。\n{CaveatSingle}";

        public static object ResultAll(int count)
            => new { mode = "all", count };

        public static object ResultSingle(Server.Game.Player target, int targetId)
            => new { mode = "single", pid = targetId, name = target.Name ?? "" };
    }
}
