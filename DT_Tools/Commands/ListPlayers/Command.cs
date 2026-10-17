using System;
using DT_Tools.Commands;
using DT_Tools.Core.Attributes;

namespace DT_Tools.Commands.ListPlayers
{
    /// <summary>
    /// /list_players [参数…] — 列出房间内所有玩家，每个参数显示一条信息并按顺序拼接；不带参数显示参数说明。
    /// </summary>
    internal sealed class ListPlayersCommand : ICommand
    {
        public string Name => "list_players";
        public string[] Aliases => new[] { "list", "who", "list_player" };
        public string Usage => "list_players [参数…]";
        public string Description => "列出所有玩家，每个参数显示一条信息并按顺序拼接（id name character role transport ip steamid steamname ping all）；不带参数显示参数说明。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.PlayerIdentity | FeatureTag.SysNetwork;

        public bool RequireHost => false;

        public CommandResult Execute(CommandContext ctx)
        {
            if (!ListPlayersArgs.TryParse(ctx.Args, out var modules, out string parseError))
            {
                ctx.Reply($"{parseError}\n用法: /list_players [参数…]（不带参数查看全部参数）");
                return CommandResult.Fail("invalid args");
            }
            if (modules.Count == 0)
            {
                ctx.Reply(ListPlayersFormat.Help());
                return CommandResult.Success(ListPlayersFormat.HelpResult());
            }

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

            ctx.Reply(ListPlayersFormat.Reply(players, hostId, modules));
            return CommandResult.Success(ListPlayersFormat.Result(players, hostId, myId));
        }
    }
}
