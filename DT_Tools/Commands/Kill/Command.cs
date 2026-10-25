using System.Collections.Generic;
using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using DT_Tools.Game;
using Protocol;
using Server.Game;

namespace DT_Tools.Commands.Kill
{
    internal sealed class KillCommand : ICommand
    {
        public string Name => "kill";
        public string[] Aliases => new[] { "处决", "击杀" };
        public string Usage => "kill <all|#playerId>";
        public string Description => "颈环炸弹处决指定/全体存活玩家（需房主·Survive/Detective/Trial 阶段，立即倒地、6 秒后死亡）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameKill;

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply($"用法: /{Usage}（可用 /list_alive 查看存活玩家列表）。");
                return CommandResult.Fail("missing target");
            }
            if (!KillArgs.TryParse(ctx.Args[0], out var target, out string parseError))
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
            if (!KillLogic.TryGuard(room, out string guardCode, out string guardText))
            {
                ctx.Reply(guardText);
                return CommandResult.Fail(guardCode);
            }

            bool isTrial = room.State == EGameState.Trial;
            var trialBlack = isTrial ? room.Trial?.Black : null;

            var targets = new List<Server.Game.Player>();
            if (target.All)
            {
                targets = PlayerQuery.AliveTargets(room);
                if (targets.Count == 0)
                {
                    ctx.Reply("当前没有存活的非观战玩家。");
                    return CommandResult.Fail("no alive players");
                }
            }
            else
            {
                var one = PlayerQuery.FindById(room, target.PlayerId);
                if (one == null)
                {
                    ctx.Reply($"找不到 PlayerId={target.PlayerId} 的玩家，可用 /list_alive 查看存活玩家列表。");
                    return CommandResult.Fail("target not found");
                }
                if (!KillLogic.IsTargetKillable(one, out string killText, out string killCode))
                {
                    ctx.Reply(killText);
                    return CommandResult.Fail(killCode);
                }
                targets.Add(one);
            }

            if (trialBlack != null && targets.Contains(trialBlack))
                ctx.Warn("⚠ 警告：处决名单中包含 Trial.Black，审判白方胜利时将无法正确处决黑方。");

            KillLogic.Execute(room, targets, isTrial);
            ctx.Reply(KillFormat.Reply(target.All, targets, isTrial));
            return CommandResult.Success(KillFormat.Result(target.All, targets, isTrial));
        }
    }
}
