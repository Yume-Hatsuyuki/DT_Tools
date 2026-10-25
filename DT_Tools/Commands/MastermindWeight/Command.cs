using System.Linq;
using DT_Tools.Core;
using DT_Tools.Core.Attributes;
using DT_Tools.Features.MastermindWeight;
using DT_Tools.Game;

namespace DT_Tools.Commands.MastermindWeight
{
    internal sealed class MastermindWeightCommand : ICommand
    {
        public string Name => "mastermind_weight";
        public string[] Aliases => new[] { "黑幕概率", "mw" };
        public string Usage => "mastermind_weight <玩家名|ID> <格子数>  |  mastermind_weight list  |  mastermind_weight reset";
        public string Description => "设置玩家的黑幕格子数（默认见配置「默认格子数」）：格子越多越可能成为黑幕，0 为不会被选中。list 查看本局概率，reset 清空。需先开启「黑幕概率」功能。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameRound | FeatureTag.PlayerIdentity;
        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply($"用法: {Usage}");
                return CommandResult.Fail("invalid args");
            }

            var room = Server.Game.GameRoom.Instance;
            if (room == null)
            {
                ctx.Reply("当前不在房间内。");
                return CommandResult.Fail("no room");
            }

            bool enabled = Engine.EnabledOf(typeof(MastermindWeightFeature));
            string head = ctx.Args[0].Trim().ToLowerInvariant();

            if (head == "list")
                return List(ctx, room, enabled);

            if (head == "reset")
            {
                MastermindWeights.Clear();
                ctx.Reply($"已清空所有单独设置，恢复为默认 {MastermindWeightFeature.DefaultWeight} 个格子。");
                return CommandResult.Success(new { reset = true });
            }

            if (ctx.Args.Length < 2)
            {
                ctx.Reply($"用法: {Usage}");
                return CommandResult.Fail("invalid args");
            }

            if (!PlayerLookup.TryResolve(room.Players, ctx.Args[0], out var target, out string lookupError))
            {
                ctx.Reply(lookupError);
                return CommandResult.Fail("player not found", new { error = lookupError });
            }

            if (!int.TryParse(ctx.Args[1], out int tickets) || tickets < 0)
            {
                ctx.Reply($"格子数必须是不小于 0 的整数（上限 {MastermindWeights.MaxWeight}）。");
                return CommandResult.Fail("invalid weight");
            }

            MastermindWeights.Set(target.AccountID, tickets);
            int applied = MastermindWeights.Get(target.AccountID, MastermindWeightFeature.DefaultWeight);
            ctx.Reply($"已设置 {target.Name} 的黑幕格子数为 {applied}。{Summary(room)}");
            if (!enabled)
                ctx.Warn("「黑幕概率」功能未开启，本次设置暂不生效。");
            return CommandResult.Success(new { name = target.Name, tickets = applied, enabled });
        }

        private static CommandResult List(CommandContext ctx, Server.Game.GameRoom room, bool enabled)
        {
            var pool = Pool(room);
            if (pool.Count == 0)
            {
                ctx.Reply("本局没有可抽取的玩家。");
                return CommandResult.Success(new { count = 0 });
            }

            var weights = Weights(pool);
            var rows = pool.Select((p, i) => new
            {
                id = p.PublicInfo.PlayerId,
                name = p.Name,
                tickets = weights[i],
                share = MastermindWeights.Share(weights, i),
            }).ToList();

            foreach (var row in rows)
                ctx.Reply($"  {row.name} (ID {row.id})：{row.tickets} 格，概率约 {row.share:P0}");
            if (!enabled)
                ctx.Warn("「黑幕概率」功能未开启，以下概率仅为预览，不影响原版抽取。");
            return CommandResult.Success(new { count = rows.Count, enabled, players = rows });
        }

        private static string Summary(Server.Game.GameRoom room)
        {
            var pool = Pool(room);
            if (pool.Count == 0)
                return string.Empty;
            var weights = Weights(pool);
            return string.Join("，", pool.Select((p, i) => $"{p.Name} {MastermindWeights.Share(weights, i):P0}"));
        }

        private static System.Collections.Generic.List<Server.Game.Player> Pool(Server.Game.GameRoom room) =>
            room.Players.Where(p => p != null && p.PublicInfo != null && !p.IsSpectator).ToList();

        private static System.Collections.Generic.List<int> Weights(System.Collections.Generic.List<Server.Game.Player> pool) =>
            pool.Select(p => MastermindWeights.Get(p.AccountID, MastermindWeightFeature.DefaultWeight)).ToList();
    }
}
