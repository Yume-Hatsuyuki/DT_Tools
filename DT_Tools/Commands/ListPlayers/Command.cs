using System;
using DT_Tools.Commands;

namespace DT_Tools.Commands.ListPlayers
{
    /// <summary>/list_players — 列出房间内所有玩家的 PlayerId / SteamId / 昵称，并标注房主。</summary>
    internal sealed class ListPlayersCommand : ICommand
    {
        public string Name => "list_players";
        public string[] Aliases => new[] { "list", "who" };
        public string Usage => "list_players";
        public string Description => "列出所有玩家的 PlayerId / SteamId / 昵称，并标注房主。";
        public string Author => "梦初雪";

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (Managers.Player == null)
            {
                ctx.Reply("玩家管理器尚未初始化（可能还未进入房间）。");
                return CommandResult.Fail("player manager not ready",
                    new { count = 0, players = Array.Empty<object>() });
            }

            var all = Managers.Player.GetAllPlayers();
            if (all == null || all.Count == 0)
            {
                ctx.Reply("当前没有已知玩家。");
                return CommandResult.Success(new
                {
                    count = 0,
                    hostId = 0,
                    myId = Managers.Player.MyPlayerID,
                    players = Array.Empty<object>(),
                });
            }

            int hostId = ListPlayersLogic.ResolveHostPlayerId();
            int myId = Managers.Player.MyPlayerID;
            var players = ListPlayersLogic.Collect(hostId, myId);

            ctx.Reply(ListPlayersFormat.Reply(players, hostId));
            return CommandResult.Success(ListPlayersFormat.Result(players, hostId, myId));
        }
    }
}
