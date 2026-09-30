using DT_Tools.Commands;
using Server.Game;

namespace DT_Tools.Commands.Kick
{
    /// <summary>
    /// /kick #&lt;playerId&gt; — 将指定玩家踢出当前房间（仅 Lobby、需房主）。
    ///
    /// 直接调用 GameRoom.KickPlayer（0.1.15b Server.Game/GameRoom.cs:1544），
    /// 与游戏内房主踢人按钮走同一套逻辑：校验 Lobby 与 Host → 目标 SteamId 进黑名单
    /// → 向目标发 S_KICKED 并断开连接。
    /// </summary>
    internal sealed class KickCommand : ICommand
    {
        public string Name => "kick";
        public string[] Aliases => new[] { "boot", "踢" };
        public string Usage => "kick #<playerId>";
        public string Description => "将指定玩家踢出房间（仅大厅可用，需房主）。";
        public string Author => "梦初雪";

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply("用法: /kick #<playerId>\n可用 /list_players 查看玩家列表。");
                return CommandResult.Fail("missing target");
            }
            if (!KickArgs.TryParse(ctx.Args[0], out int targetId, out string parseError))
            {
                ctx.Reply(parseError);
                return CommandResult.Fail("invalid target");
            }

            var room = GameRoom.Instance;
            if (room == null)
            {
                ctx.Reply("当前没有活动的游戏房间。");
                return CommandResult.Fail("no room");
            }
            if (!KickLogic.TryGuard(room, out string guardCode, out string guardText))
            {
                ctx.Reply(guardText);
                return CommandResult.Fail(guardCode);
            }
            if (!KickLogic.TryResolveTarget(room, targetId, out var target, out string targetCode, out string targetText))
            {
                ctx.Reply(targetText);
                return CommandResult.Fail(targetCode);
            }

            string targetName = KickFormat.DisplayName(target, targetId);
            // 与游戏内踢人按钮相同路径（room.Host 已在 TryResolveTarget 校验非空）
            room.KickPlayer(room.Host, targetId);
            ctx.Reply($"已踢出玩家 {targetName}（#{targetId}）。");
            return CommandResult.Success(KickFormat.Result(target, targetId));
        }
    }
}
