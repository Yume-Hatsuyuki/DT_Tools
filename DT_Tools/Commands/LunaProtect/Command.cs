using System.Collections.Generic;
using DT_Tools.Commands;
using DT_Tools.Core.Attributes;
using DT_Tools.Game;
using Server.Game;

namespace DT_Tools.Commands.LunaProtect
{
    internal sealed class LunaProtectCommand : ICommand
    {
        public string Name => "luna_protect";
        public string[] Aliases => new[] { "protect", "护盾", "免死", "保护" };
        public string Usage => "luna_protect <all|#playerId>";
        public string Description => "给指定/全体存活玩家套用 Luna 护盾（亮灯时不可被 Black 刀杀，需房主·生存阶段）。";
        public string Author => "梦初雪";
        public FeatureTag Tags => FeatureTag.GameBuff | FeatureTag.GameKill;

        public bool RequireHost => true;

        public CommandResult Execute(CommandContext ctx)
        {
            if (ctx.Args.Length == 0)
            {
                ctx.Reply("用法: /luna_protect <all|#playerId>\n可用 /list_alive 查看存活玩家列表。");
                return CommandResult.Fail("missing target");
            }
            if (!LunaProtectArgs.TryParse(ctx.Args[0], out var args, out string parseError))
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
            if (!LunaProtectLogic.TryGuard(room, out string guardCode, out string guardText))
            {
                ctx.Reply(guardText);
                return CommandResult.Fail(guardCode);
            }

            if (args.All)
            {
                var targets = PlayerQuery.AliveTargets(room);
                if (targets.Count == 0)
                {
                    ctx.Reply("当前没有存活的非观战玩家。");
                    return CommandResult.Fail("no alive players");
                }
                LunaProtectLogic.Apply(room, targets);
                ctx.Reply(LunaProtectFormat.ReplyAll(targets.Count));
                return CommandResult.Success(LunaProtectFormat.ResultAll(targets.Count));
            }

            var target = PlayerQuery.FindById(room, args.PlayerId);
            if (target == null)
            {
                ctx.Reply($"找不到 PlayerId={args.PlayerId} 的玩家，可用 /list_alive 查看玩家列表。");
                return CommandResult.Fail("target not found");
            }
            if (!LunaProtectLogic.IsTargetProtectable(target, args.PlayerId, out string targetCode, out string targetText))
            {
                ctx.Reply(targetText);
                return CommandResult.Fail(targetCode);
            }

            LunaProtectLogic.Apply(room, new List<Server.Game.Player> { target });
            ctx.Reply(LunaProtectFormat.ReplySingle(target, args.PlayerId));
            return CommandResult.Success(LunaProtectFormat.ResultSingle(target, args.PlayerId));
        }
    }
}
